using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using WebAPI.Middleware;

internal sealed class SheetTransportState
{
    public TaskCompletionSource Connected { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Disconnected { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}

[Authorize]
internal sealed class SheetTransportTestHub(SheetTransportState state) : Hub
{
    public string Ping() => Context.User?.Identity?.IsAuthenticated == true ? "authenticated" : "anonymous";

    public override Task OnConnectedAsync()
    {
        state.Connected.TrySetResult();
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        state.Disconnected.TrySetResult();
        return base.OnDisconnectedAsync(exception);
    }
}

internal static class SheetTransportTests
{
    private const string Origin = "https://flowassist.mgs.com.tr";
    private const string HubPath = "/api/sheets-hub";
    private const string DisconnectPath = HubPath + "/disconnect";

    public static async Task<int> Run()
    {
        await VerifyTranslationScopeAndRestoration();

        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSignalR();
        var state = new SheetTransportState();
        builder.Services.AddSingleton(state);
        builder.Services.AddCors(options => options.AddPolicy("CorsPolicy", policy =>
            policy.WithOrigins(Origin).AllowAnyMethod().AllowAnyHeader().AllowCredentials()));
        var key = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32));
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(jwt =>
        {
            jwt.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    if (context.Request.Path.StartsWithSegments(HubPath))
                        context.Token = context.Request.Query["access_token"];
                    return Task.CompletedTask;
                }
            };
            jwt.TokenValidationParameters = new()
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = "sheets-transport-tests",
                ValidAudience = "sheets-transport-tests",
                IssuerSigningKey = key,
                ClockSkew = TimeSpan.Zero
            };
        });
        builder.Services.AddAuthorization();
        await using var app = builder.Build();
        app.UseMiddleware<SheetsHubDisconnectMiddleware>();
        app.UseRouting();
        app.UseCors("CorsPolicy");
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapHub<SheetTransportTestHub>(HubPath, hub => hub.CloseOnAuthenticationExpiration = true);
        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient { BaseAddress = new Uri(address), Timeout = TimeSpan.FromSeconds(10) };
            var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
                "sheets-transport-tests", "sheets-transport-tests", [new Claim(ClaimTypes.NameIdentifier, "1")],
                expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)));

            using var preflight = new HttpRequestMessage(HttpMethod.Options, DisconnectPath + "?id=test");
            preflight.Headers.Add("Origin", Origin);
            preflight.Headers.Add("Access-Control-Request-Method", "POST");
            preflight.Headers.Add("Access-Control-Request-Headers", "authorization,x-requested-with,x-signalr-user-agent");
            using var preflightResponse = await client.SendAsync(preflight);
            Check.That(preflightResponse.StatusCode == HttpStatusCode.NoContent, "POST disconnect CORS preflight");
            Check.That(Header(preflightResponse, "Access-Control-Allow-Origin") == Origin &&
                Header(preflightResponse, "Access-Control-Allow-Credentials") == "true" &&
                Header(preflightResponse, "Access-Control-Allow-Methods").Contains("POST") &&
                Header(preflightResponse, "Access-Control-Allow-Headers").Contains("authorization"), "POST disconnect CORS headers and credentials");

            using var unauthorized = await Send(client, HttpMethod.Post, DisconnectPath + "?id=test");
            Check.That(unauthorized.StatusCode == HttpStatusCode.Unauthorized &&
                Header(unauthorized, "Access-Control-Allow-Origin") == Origin, "POST disconnect still requires JWT and returns CORS headers");

            using var forbiddenOrigin = new HttpRequestMessage(HttpMethod.Options, DisconnectPath);
            forbiddenOrigin.Headers.Add("Origin", "https://untrusted.example");
            forbiddenOrigin.Headers.Add("Access-Control-Request-Method", "POST");
            using var forbiddenResponse = await client.SendAsync(forbiddenOrigin);
            Check.That(!forbiddenResponse.Headers.Contains("Access-Control-Allow-Origin"), "Unapproved origins are not allowed");

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var negotiate = await Send(client, HttpMethod.Post, HubPath + "/negotiate?negotiateVersion=1");
            negotiate.EnsureSuccessStatusCode();
            using var negotiation = JsonDocument.Parse(await negotiate.Content.ReadAsStringAsync());
            var connection = "?id=" + Uri.EscapeDataString(negotiation.RootElement.GetProperty("connectionToken").GetString()!);
            using var initialPoll = await Send(client, HttpMethod.Get, HubPath + connection);
            Check.That(initialPoll.StatusCode == HttpStatusCode.OK, "Authenticated long polling starts");
            using var handshake = await Send(client, HttpMethod.Post, HubPath + connection, "{\"protocol\":\"json\",\"version\":1}\u001e");
            handshake.EnsureSuccessStatusCode();
            using var handshakePoll = await Send(client, HttpMethod.Get, HubPath + connection);
            var frames = await handshakePoll.Content.ReadAsStringAsync();
            Check.That(frames.Contains("{}\u001e"), "Long polling JSON handshake completes");
            await state.Connected.Task.WaitAsync(TimeSpan.FromSeconds(5));

            using var invoke = await Send(client, HttpMethod.Post, HubPath + connection,
                "{\"type\":1,\"invocationId\":\"ping\",\"target\":\"Ping\",\"arguments\":[]}\u001e");
            invoke.EnsureSuccessStatusCode();
            using var invocationPoll = await Send(client, HttpMethod.Get, HubPath + connection);
            Check.That((await invocationPoll.Content.ReadAsStringAsync()).Contains("authenticated"), "Authenticated hub invocation over long polling");

            client.DefaultRequestHeaders.Authorization = null;
            using var deniedDisconnect = await Send(client, HttpMethod.Post, DisconnectPath + connection);
            Check.That(deniedDisconnect.StatusCode == HttpStatusCode.Unauthorized, "Anonymous requests cannot disconnect an active connection");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var pendingPoll = Send(client, HttpMethod.Get, HubPath + connection);
            using var disconnect = await Send(client, HttpMethod.Post, DisconnectPath + connection);
            Check.That(disconnect.StatusCode is HttpStatusCode.Accepted or HttpStatusCode.OK &&
                Header(disconnect, "Access-Control-Allow-Origin") == Origin, "POST disconnect cleans up SignalR with CORS");
            using var stoppedPoll = await pendingPoll.WaitAsync(TimeSpan.FromSeconds(5));
            Check.That(stoppedPoll.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.NotFound, "Disconnect ends outstanding long poll");
            await state.Disconnected.Task.WaitAsync(TimeSpan.FromSeconds(5));
            using var removedPoll = await Send(client, HttpMethod.Get, HubPath + connection);
            Check.That(removedPoll.StatusCode == HttpStatusCode.NotFound, "Disconnected connection is removed");
            using var repeat = await Send(client, HttpMethod.Post, DisconnectPath + connection);
            Check.That(repeat.StatusCode == HttpStatusCode.NotFound, "Repeated disconnect returns not found");
            using var missingId = await Send(client, HttpMethod.Post, DisconnectPath);
            Check.That(missingId.StatusCode == HttpStatusCode.BadRequest, "Disconnect without connection id is rejected");
        }
        finally
        {
            await app.StopAsync();
        }

        Console.WriteLine("PASS POST-only SignalR long-poll disconnect, CORS, JWT and scoped middleware restoration (no database)");
        return 9;
    }

    private static string Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? string.Join(",", values) : "";

    private static async Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string path, string? content = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add("Origin", Origin);
        if (content != null) request.Content = new StringContent(content, Encoding.UTF8, "text/plain");
        return await client.SendAsync(request);
    }

    private static async Task VerifyTranslationScopeAndRestoration()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = DisconnectPath;
        context.Request.PathBase = "/application";
        context.Request.QueryString = new QueryString("?id=retained&other=value");
        var middleware = new SheetsHubDisconnectMiddleware(next =>
        {
            Check.That(next.Request.Method == HttpMethods.Delete && next.Request.Path == HubPath &&
                next.Request.PathBase == "/application" && next.Request.QueryString.Value == "?id=retained&other=value",
                "Only internal hub method/path is translated");
            throw new InvalidOperationException("Expected downstream failure");
        });
        var threw = false;
        try { await middleware.InvokeAsync(context); }
        catch (InvalidOperationException ex) when (ex.Message == "Expected downstream failure") { threw = true; }
        Check.That(threw && context.Request.Method == HttpMethods.Post && context.Request.Path == DisconnectPath,
            "Original POST method/path are restored after downstream failure");

        foreach (var (method, path) in new[]
        {
            (HttpMethods.Post, HubPath), (HttpMethods.Get, DisconnectPath),
            (HttpMethods.Put, DisconnectPath), (HttpMethods.Post, DisconnectPath + "/other")
        })
        {
            context.Request.Method = method;
            context.Request.Path = path;
            var passThrough = new SheetsHubDisconnectMiddleware(next =>
            {
                Check.That(next.Request.Method == method && next.Request.Path == path, "Unrelated hub requests retain method/path");
                return Task.CompletedTask;
            });
            await passThrough.InvokeAsync(context);
        }
    }
}
