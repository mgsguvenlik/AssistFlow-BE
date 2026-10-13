using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Business.Interfaces;
using Business.Interfaces.Sheets;
using Business.Services.Sheets;
using Business.UnitOfWork;
using Data.Abstract;
using Data.Concrete;
using Data.Concrete.EfCore.Context;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Model.Dtos.Auth;
using WebAPI.Controllers;
using WebAPI.Hubs;
using WebAPI.Middleware;

internal sealed class HttpTestUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public long Id => long.TryParse(accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    public string? Email => null;
    public string? Name => accessor.HttpContext?.User.Identity?.Name;
    public ValueTask<CurrentUserDto?> GetAsync(CancellationToken ct = default) => ValueTask.FromResult<CurrentUserDto?>(new() { Id = Id, IsAuthenticated = Id > 0 });
}

internal static class SheetHttpTests
{
    public static async Task Run(DbContextOptions<AppDataContext> options, Guid workbookId, long owner, long viewer, long outsider)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddControllers().AddApplicationPart(typeof(SheetsController).Assembly);
        builder.Services.AddSignalR();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<AppDataContext>(_ => new SheetsTestContext(options));
        builder.Services.AddScoped<IRepository>(sp => new Repository(sp.GetRequiredService<AppDataContext>()));
        builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
        builder.Services.AddScoped<ICurrentUser, HttpTestUser>();
        builder.Services.AddScoped<ISheetsService, SheetsService>();
        var key = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32));
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(jwt =>
        {
            jwt.TokenValidationParameters = new() { ValidateIssuer = true, ValidateAudience = true, ValidateIssuerSigningKey = true,
                ValidIssuer = "sheets-tests", ValidAudience = "sheets-tests", IssuerSigningKey = key,
                NameClaimType = ClaimTypes.Name, ClockSkew = TimeSpan.Zero };
            jwt.Events = new JwtBearerEvents {
                OnMessageReceived = context => {
                    if (context.Request.Path.StartsWithSegments("/api/sheets-hub"))
                        context.Token = context.Request.Query["access_token"];
                    return Task.CompletedTask;
                }
            };
        });
        builder.Services.AddAuthorization();
        await using var app = builder.Build();
        app.UseMiddleware<ErrorHandlerMiddleware>();
        app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
        app.MapHub<SheetsHub>("/api/sheets-hub", hub => hub.CloseOnAuthenticationExpiration = true);
        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient { BaseAddress = new Uri(address) };
            string Token(long user) => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("sheets-tests", "sheets-tests",
                [new Claim(ClaimTypes.NameIdentifier, user.ToString()), new Claim(ClaimTypes.Name, "Test " + user)],
                expires: DateTime.UtcNow.AddMinutes(10), signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)));
            Check.That((await client.GetAsync("/api/Sheets/" + workbookId)).StatusCode == HttpStatusCode.Unauthorized, "HTTP authentication");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(outsider));
            Check.That((await client.GetAsync("/api/Sheets/" + workbookId)).StatusCode == HttpStatusCode.NotFound, "HTTP visibility");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(viewer));
            Check.That((await client.GetAsync("/api/Sheets/" + workbookId + "/export")).StatusCode == HttpStatusCode.Forbidden, "HTTP protected export");
            Check.That((await client.PostAsJsonAsync("/api/Sheets/" + workbookId + "/save", new { baseRevisionId = Guid.NewGuid() })).StatusCode == HttpStatusCode.Forbidden, "HTTP protected save");

            using var activitiesResponse = await client.GetAsync($"/api/Sheets/{workbookId}/activities");
            activitiesResponse.EnsureSuccessStatusCode();
            using var activitiesJson = JsonDocument.Parse(await activitiesResponse.Content.ReadAsStringAsync());
            var activities = activitiesJson.RootElement.GetProperty("data").GetProperty("items").EnumerateArray().ToArray();
            Check.That(activities.Length > 0 && activities.All(x => x.GetProperty("occurredAtUtc").GetString()!.EndsWith("Z")),
                "HTTP activity timestamps retain the UTC suffix after SQL storage");

            async Task<ClientWebSocket> Connect(long user)
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(user));
                using var negotiate = await client.PostAsync("/api/sheets-hub/negotiate?negotiateVersion=1", null);
                negotiate.EnsureSuccessStatusCode();
                using var payload = JsonDocument.Parse(await negotiate.Content.ReadAsStringAsync());
                var connectionToken = payload.RootElement.GetProperty("connectionToken").GetString()!;
                var ws = new ClientWebSocket();
                await ws.ConnectAsync(new Uri(address.Replace("http://", "ws://") + "/api/sheets-hub?id=" + Uri.EscapeDataString(connectionToken) + "&access_token=" + Uri.EscapeDataString(Token(user))), default);
                return ws;
            }
            using var outsiderSocket = await Connect(outsider);
            var outsiderProtocol = new SignalProtocol(outsiderSocket);
            await outsiderProtocol.Send(new { protocol = "json", version = 1 });
            await outsiderProtocol.ReadUntil(x => !x.TryGetProperty("type", out _));
            await outsiderProtocol.Send(new { type = 1, invocationId = "out", target = "Join", arguments = new[] { workbookId } });
            var rejected = await outsiderProtocol.ReadUntil(x => x.TryGetProperty("invocationId", out var invocation) && invocation.GetString() == "out");
            Check.That(rejected.TryGetProperty("error", out _), "SignalR uninvited user cannot join");

            using var viewerSocket = await Connect(viewer);
            var protocol = new SignalProtocol(viewerSocket);
            await protocol.Send(new { protocol = "json", version = 1 });
            await protocol.ReadUntil(x => !x.TryGetProperty("type", out _));
            await protocol.Send(new { type = 1, invocationId = "join", target = "Join", arguments = new[] { workbookId } });
            var joined = await protocol.ReadUntil(x => x.TryGetProperty("invocationId", out var invocation) && invocation.GetString() == "join");
            Check.That(!joined.TryGetProperty("error", out _), "SignalR authorized presence");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(owner));
            using var create = await client.PostAsJsonAsync("/api/Sheets", new { name = "HTTP create and open", rows = 100, columns = 26 });
            create.EnsureSuccessStatusCode();
            using var created = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
            var createdData = created.RootElement.GetProperty("data");
            var createdId = createdData.GetProperty("id").GetGuid();
            using var opened = await client.GetAsync($"/api/Sheets/{createdId}");
            opened.EnsureSuccessStatusCode();
            using var openedBook = JsonDocument.Parse(await opened.Content.ReadAsStringAsync());
            var openedData = openedBook.RootElement.GetProperty("data");
            using var window = await client.PostAsJsonAsync($"/api/Sheets/{createdId}/window", new {
                revisionId = openedData.GetProperty("revisionId").GetGuid(),
                worksheetId = openedData.GetProperty("worksheets")[0].GetProperty("id").GetGuid(), startRow = 0, count = 128 });
            window.EnsureSuccessStatusCode();
            using var firstWindow = JsonDocument.Parse(await window.Content.ReadAsStringAsync());
            Check.That(firstWindow.RootElement.GetProperty("data").GetProperty("totalRows").GetInt32() == 100, "HTTP creation opens the editor row window");
            using var revoke = await client.PostAsync($"/api/Sheets/{workbookId}/access/{viewer}?grant=false", null);
            revoke.EnsureSuccessStatusCode();
            var changed = await protocol.ReadUntil(x => x.TryGetProperty("target", out var target) && target.GetString() == "AccessChanged");
            Check.That(changed.GetProperty("type").GetInt32() == 1, "SignalR revocation notification");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(viewer));
            Check.That((await client.GetAsync("/api/Sheets/" + workbookId)).StatusCode == HttpStatusCode.NotFound, "Revocation applies immediately to HTTP");
            viewerSocket.Abort(); outsiderSocket.Abort();

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(owner));
            using var excel = SheetExcelFixture.Create();
            var bytes = excel.ToArray();
            using var form = new MultipartFormDataContent();
            form.Add(new ByteArrayContent(bytes), "file", "fixture.xlsx");
            form.Add(new StringContent("HTTP Excel fixture"), "name");
            using var upload = await client.PostAsync("/api/Sheets/import", form);
            upload.EnsureSuccessStatusCode();
            using var uploaded = JsonDocument.Parse(await upload.Content.ReadAsStringAsync());
            var data = uploaded.RootElement.GetProperty("data");
            var importedId = data.GetProperty("id").GetGuid();
            var revision = data.GetProperty("revisionId").GetGuid();
            using var download = await client.GetAsync($"/api/Sheets/{importedId}/export");
            download.EnsureSuccessStatusCode();
            Check.That(download.Content.Headers.ContentType?.MediaType == "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "HTTP XLSX download content type");
            using var append = new MultipartFormDataContent();
            append.Add(new ByteArrayContent(bytes), "file", "append.xlsx");
            append.Add(new StringContent(JsonSerializer.Serialize(new { baseRevisionId = revision })), "draft");
            using var appended = await client.PostAsync($"/api/Sheets/{importedId}/import", append);
            appended.EnsureSuccessStatusCode();
            using var draft = JsonDocument.Parse(await appended.Content.ReadAsStringAsync());
            Check.That(draft.RootElement.GetProperty("data").GetProperty("worksheets").GetArrayLength() == 2, "HTTP multipart import appends draft worksheet");
            using var current = await client.GetAsync($"/api/Sheets/{importedId}");
            using var active = JsonDocument.Parse(await current.Content.ReadAsStringAsync());
            Check.That(active.RootElement.GetProperty("data").GetProperty("revisionId").GetGuid() == revision, "HTTP append waits for Save");
        }
        finally { await app.StopAsync(); }
        Console.WriteLine("PASS authenticated HTTP endpoints and SignalR join/revocation");
    }

    private sealed class SignalProtocol(ClientWebSocket socket)
    {
        private string pending = "";
        private readonly Queue<JsonElement> frames = new();
        public async Task Send(object value)
        {
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value) + "\u001e");
            await socket.SendAsync(bytes, WebSocketMessageType.Text, true, default);
        }
        public async Task<JsonElement> ReadUntil(Func<JsonElement, bool> predicate)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var buffer = new byte[8192];
            while (true)
            {
                while (frames.TryDequeue(out var frame)) if (predicate(frame)) return frame;
                var received = await socket.ReceiveAsync(buffer, timeout.Token);
                if (received.MessageType == WebSocketMessageType.Close) throw new Exception("SignalR closed before expected response");
                pending += Encoding.UTF8.GetString(buffer, 0, received.Count);
                int separator;
                while ((separator = pending.IndexOf('\u001e')) >= 0)
                {
                    var json = pending[..separator]; pending = pending[(separator + 1)..];
                    if (json.Length != 0) { using var document = JsonDocument.Parse(json); frames.Enqueue(document.RootElement.Clone()); }
                }
            }
        }
    }
}
