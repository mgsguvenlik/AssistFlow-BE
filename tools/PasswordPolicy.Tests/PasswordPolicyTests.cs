using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Business.Interfaces;
using Business.Models;
using Business.Services;
using Business.UnitOfWork;
using Core.Common;
using Core.Settings.Concrete;
using Core.Utilities.Security;
using Data.Concrete;
using Data.Concrete.EfCore.Context;
using Data.Seeding.Seeds;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Model.Abstractions;
using Model.Concrete;
using Model.Dtos.Auth;
using Model.Dtos.Menu;
using Model.Dtos.User;
using WebAPI.Middleware;
using WebAPI.Authorization;

internal static class PasswordPolicyTests
{
    private static int checks;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("FAIL: " + message);
        checks++;
    }
    public static async Task Main()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new PolicyTestContext(new DbContextOptionsBuilder<AppDataContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var hasher = new PasswordHasher<User>();
        var legacy = new User { Code = "LEGACY", Name = "Existing user", CreatedDate = DateTimeOffset.UtcNow.AddYears(-2), IsActive = true };
        legacy.PasswordHash = hasher.HashPassword(legacy, "OldPass123");
        db.Add(legacy);
        await db.SaveChangesAsync();
        using var provider = new ServiceCollection().BuildServiceProvider();
        var seed = new PasswordPolicySeed();
        var beforeSeed = DateTimeOffset.UtcNow;
        await seed.RunAsync(db, provider, default);
        await db.Entry(legacy).ReloadAsync();
        Check(!legacy.MustChangePassword && legacy.PasswordVersion == 0, "existing users are not forced on rollout");
        Check(legacy.PasswordChangedAt >= beforeSeed && legacy.PasswordChangedAt <= DateTimeOffset.UtcNow, "existing password starts at seed execution date");
        var parameter = await db.Set<Configuration>().SingleAsync();
        Check(parameter.Name == "PasswordValidityDays" && parameter.Value == "180", "default parameter seeded");
        var originalDate = legacy.PasswordChangedAt;
        parameter.Value = "90";
        legacy.MustChangePassword = true;
        await db.SaveChangesAsync();
        await seed.RunAsync(db, provider, default);
        await db.Entry(legacy).ReloadAsync();
        Check(legacy.PasswordChangedAt == originalDate && legacy.MustChangePassword, "repeat seed preserves date and pending request");
        Check((await db.Set<Configuration>().SingleAsync()).Value == "90", "repeat seed preserves customized parameter");
        legacy.MustChangePassword = false;
        parameter.Value = "180";
        await db.SaveChangesAsync();

        var settings = new SettingsSnapshot(new AppSettings
        {
            MSSQLConnectionString = "", PostgresConnectionString = "", Issuer = "isolated-policy-tests", Audience = "isolated-policy-tests",
            Key = "LOCAL_TEST_KEY_ONLY_0123456789012345678901234567890123456789", OpenidConfiguration = "", DbProvider = "",
            AppUrl = "https://example.invalid", FrontUrl = "https://example.invalid", ManitouBaseUrl = "", ManitouPassword = "", AccessTokenMinutes = 60
        });
        var uow = new UnitOfWork(new Repository(db));
        var policy = new PasswordPolicyService(uow, settings);
        var mail = new ForbiddenMail();
        var notifier = new TestNotifier();
        var config = new TypeAdapterConfig();
        new Business.Mapper.MapsterConfig().Register(config);
        var users = new UserService(uow, new Mapper(config), config, hasher, mail, policy, notifier);
        var auth = new AuthService(new HttpContextAccessor(), users, settings, DispatchProxy.Create<IMenuService, MenuProxy>(), policy);
        var signedIn = await auth.LoginAsync(new() { Identifier = "LEGACY", Password = "OldPass123" });
        Check(signedIn.IsSuccess && !signedIn.Data!.RequiresPasswordChange && signedIn.Data.Token.Length > 0, "legacy user can log in normally");
        var access = new JwtSecurityTokenHandler().ReadJwtToken(signedIn.Data!.Token);
        Check(access.Claims.Any(x => x.Type == PasswordPolicyRules.PurposeClaim && x.Value == "access"), "normal token has access purpose");
        var wrongPassword = await auth.LoginAsync(new() { Identifier = "LEGACY", Password = "Wrong123" });
        Check(!wrongPassword.IsSuccess && wrongPassword.Data is null, "wrong password cannot obtain challenge");
        var created = await users.CreateAsync(new() { Code = "NEW", Name = "New user", Password = "FirstPass123" });
        Check(created.IsSuccess && created.Data!.MustChangePassword && created.Data.PasswordChangedAt.HasValue, "new administrator-created user must change password");
        var firstLogin = await auth.LoginAsync(new() { Identifier = "NEW", Password = "FirstPass123" });
        Check(firstLogin.IsSuccess && firstLogin.Data!.RequiresPasswordChange && firstLogin.Data.Token == "" && firstLogin.Data.Menus.Count == 0,
            "first login gets only restricted challenge, no access token or menus");
        var challenge = firstLogin.Data!.PasswordChangeToken!;
        Check(policy.ValidateToken(challenge, PasswordPolicyRules.ChangePurpose) is not null, "challenge cryptographic validation succeeds");
        Check(policy.ValidateToken(challenge, PasswordPolicyRules.ResetPurpose) is null, "challenge cannot be used for another purpose");
        var parts = challenge.Split('.');
        parts[1] = Base64UrlEncoder.Encode("{\"sub\":\"999\",\"token_use\":\"password-change\"}");
        Check(policy.ValidateToken(string.Join('.', parts), PasswordPolicyRules.ChangePurpose) is null, "tampered token is rejected");
        var expiredToken = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(issuer: settings.Value.Issuer, audience: settings.Value.Audience,
            claims: [new(ClaimTypes.NameIdentifier, created.Data!.Id.ToString()), new(PasswordPolicyRules.VersionClaim, "0"), new(PasswordPolicyRules.PurposeClaim, PasswordPolicyRules.ChangePurpose)],
            expires: DateTime.UtcNow.AddMinutes(-1), signingCredentials: new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Value.Key)), SecurityAlgorithms.HmacSha256)));
        Check(policy.ValidateToken(expiredToken, PasswordPolicyRules.ChangePurpose) is null, "expired challenge rejected using UTC");
        async Task<ResponseModel<UserGetDto>> Complete(string token, string password, string? confirm = null) =>
            await users.CompleteRequiredPasswordChangeAsync(new() { PasswordChangeToken = token, NewPassword = password, NewPasswordConfirm = confirm ?? password });
        Check(!(await Complete(challenge, "FirstPass123")).IsSuccess, "same password is rejected despite salted hash");
        Check(!(await Complete(challenge, "short")).IsSuccess, "weak password rejected");
        Check(!(await Complete(challenge, "NewPass123", "Other123")).IsSuccess, "mismatched confirmation rejected");
        Check((await Complete(challenge, "NewPass123")).IsSuccess, "user completes first-login change");
        Check(!(await Complete(challenge, "ReplayPass123")).IsSuccess, "challenge cannot be replayed");
        var newUser = await db.Set<User>().SingleAsync(x => x.Code == "NEW");
        Check(!newUser.MustChangePassword && newUser.PasswordVersion == 1, "user change clears flag and increments password version");
        Check((await auth.LoginAsync(new() { Identifier = "NEW", Password = "NewPass123" })).Data!.RequiresPasswordChange == false, "new password grants normal access");

        Check((await users.RequestPasswordChangeAsync(legacy.Id)).IsSuccess, "administrator requests change");
        Check(notifier.Calls == 1 && notifier.LastId == legacy.Id, "active user is notified after request");
        Check(!(await users.RequestPasswordChangeAsync(legacy.Id)).IsSuccess && notifier.Calls == 1, "pending request cannot be duplicated");
        var forcedLogin = await auth.LoginAsync(new() { Identifier = "LEGACY", Password = "OldPass123" });
        Check(forcedLogin.Data!.RequiresPasswordChange && forcedLogin.Data.Token == "", "existing user blocked after administrator request");
        async Task<(int Status, string Body, bool Passed)> Request(int? version, bool anonymous = false)
        {
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, legacy.Id.ToString()) };
            if (version.HasValue) claims.Add(new(PasswordPolicyRules.VersionClaim, version.Value.ToString()));
            context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
            if (anonymous) context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute()), "anonymous"));
            var passed = false;
            await new PasswordPolicyMiddleware(_ => { passed = true; return Task.CompletedTask; }).InvokeAsync(context, policy);
            context.Response.Body.Position = 0;
            return (context.Response.StatusCode, await new StreamReader(context.Response.Body).ReadToEndAsync(), passed);
        }
        var blocked = await Request(0);
        Check(blocked.Status == 428 && !blocked.Passed, "active session cannot call protected API after request");
        var blockedJson = JsonDocument.Parse(blocked.Body).RootElement;
        Check(blockedJson.GetProperty("code").GetString() == "PASSWORD_CHANGE_REQUIRED", "active session receives change requirement");
        Check((await Complete(blockedJson.GetProperty("passwordChangeToken").GetString()!, "ChangedPass123")).IsSuccess, "active session challenge allows self change");
        Check((await Request(0)).Status == 401, "old access token stays invalid after change");
        Check((await Request(null)).Status == 401, "pre-rollout tokens cannot bypass changed password version");
        Check((await Request(1)).Passed, "new access token may access application");
        Check((await users.RequestPasswordChangeAsync(legacy.Id)).IsSuccess, "request button available again after self change");
        Check((await users.UpdateUserPassword(legacy.Id, "AdminPass123")).IsSuccess, "existing administrator password reset retained");
        Check(legacy.MustChangePassword && legacy.PasswordVersion == 2, "administrator reset does not clear user's obligation");
        Check((await Request(1)).Status == 401, "administrator password reset revokes old access token");
        var adminResetLogin = await auth.LoginAsync(new() { Identifier = "LEGACY", Password = "AdminPass123" });
        Check(adminResetLogin.Data!.RequiresPasswordChange, "administrator-set password still requires self change");
        Check((await Complete(adminResetLogin.Data.PasswordChangeToken!, "OwnPass123")).IsSuccess, "user clears administrator reset obligation");
        legacy.PasswordChangedAt = DateTimeOffset.UtcNow.AddDays(-181);
        await db.SaveChangesAsync();
        Check((await policy.GetAsync(legacy.Id))!.RequiresChange, "password expires after 180 days");
        Check((await Request(3)).Status == 428, "expired active session blocked by API");
        var expiredLogin = await auth.LoginAsync(new() { Identifier = "LEGACY", Password = "OwnPass123" });
        Check(expiredLogin.Data!.RequiresPasswordChange, "expired password cannot obtain full login");
        parameter.Value = "365";
        await db.SaveChangesAsync();
        Check(!(await policy.GetAsync(legacy.Id))!.RequiresChange, "parameter modification immediately changes expiry");
        parameter.Value = "bad";
        await db.SaveChangesAsync();
        Check((await policy.GetAsync(legacy.Id))!.RequiresChange, "invalid stored configuration safely falls back to 180 days");
        parameter.Value = "180";
        legacy.PasswordChangedAt = DateTimeOffset.UtcNow.AddDays(-180).AddSeconds(-1);
        await db.SaveChangesAsync();
        Check((await policy.GetAsync(legacy.Id))!.RequiresChange, "expiry at exact boundary enforced");
        legacy.PasswordChangedAt = DateTimeOffset.UtcNow.AddDays(-180).AddMinutes(1);
        await db.SaveChangesAsync();
        Check(!(await policy.GetAsync(legacy.Id))!.RequiresChange, "password valid just before deadline");
        legacy.IsActive = false;
        await db.SaveChangesAsync();
        Check(await policy.GetAsync(legacy.Id) is null && !(await auth.LoginAsync(new() { Identifier = "LEGACY", Password = "OwnPass123" })).IsSuccess, "inactive user cannot use change flow to regain access");
        legacy.IsActive = true;
        await db.SaveChangesAsync();
        Check((await Request(3, anonymous: true)).Passed, "anonymous password endpoint is not trapped by session policy");
        var configurationService = new ConfigurationService(uow, new Mapper(config), config);
        foreach (var invalid in new[] { "0", "-1", "text", "1.5", "36501" })
            Check(!(await configurationService.UpdateAsync(new() { Id = parameter.Id, Name = "PasswordValidityDays", Value = invalid })).IsSuccess, "invalid day parameter rejected: " + invalid);
        var previousDate = newUser.PasswordChangedAt;
        Check((await users.ChangePasswordWithOldAsync(newUser.Id, "NewPass123", "ProfilePass123", "ProfilePass123")).IsSuccess,
            "existing profile password change retained");
        Check(newUser.PasswordVersion == 2 && !newUser.MustChangePassword && newUser.PasswordChangedAt > previousDate,
            "profile change restarts validity and revokes old sessions");
        var reset = policy.CreateToken(newUser.Id, newUser.PasswordVersion, PasswordPolicyRules.ResetPurpose, out _);
        Check(!(await users.ChangePasswordAsync(challenge, "ResetPass123")).IsSuccess, "reset endpoint rejects wrong token purpose");
        Check((await users.ChangePasswordAsync(reset, "ResetPass123")).IsSuccess, "existing reset endpoint accepts signed reset token");
        Check(!(await users.ChangePasswordAsync(reset, "ReplayReset123")).IsSuccess, "reset token cannot be replayed");
        Check(newUser.PasswordVersion == 3 && !newUser.MustChangePassword, "reset completion clears requirement and revokes prior sessions");
        Check((await users.UpdateAsync(new() { Id = newUser.Id, Code = newUser.Code, Name = newUser.Name, IsActive = true, NewPassword = "DetailAdmin123" })).IsSuccess,
            "administrator detail password update retained");
        Check(newUser.PasswordVersion == 4 && newUser.MustChangePassword, "detail update leaves mandatory self-change flag enabled");
        var detailChallenge = (await auth.LoginAsync(new() { Identifier = "NEW", Password = "DetailAdmin123" })).Data!.PasswordChangeToken!;
        Check((await Complete(detailChallenge, "DetailOwn123")).IsSuccess, "detail administrator password can be replaced by user");
        await using (var concurrent = new PolicyTestContext(new DbContextOptionsBuilder<AppDataContext>().UseSqlite(connection).Options))
        {
            var stale = await concurrent.Set<User>().SingleAsync(x => x.Id == newUser.Id);
            Check((await users.RequestPasswordChangeAsync(newUser.Id)).IsSuccess, "request succeeds while another context holds stale user");
            stale.PasswordHash = hasher.HashPassword(stale, "Concurrent123");
            stale.PasswordVersion++;
            var rejected = false;
            try { await concurrent.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException) { rejected = true; }
            Check(rejected, "concurrent password write cannot lose administrator request");
            Check((await db.Set<User>().AsNoTracking().SingleAsync(x => x.Id == newUser.Id)).MustChangePassword,
                "administrator flag remains persisted after concurrent write rejection");
        }
        Check(mail.Calls == 0, "new policy workflow never sends mail");

        var adminRole = new Role { Name = "Admin", Code = "ADMIN" };
        var misleadingRole = new Role { Name = "SuperAdmin", Code = "PERSONEL" };
        db.AddRange(adminRole, misleadingRole);
        await db.SaveChangesAsync();
        var adminAssignment = new UserRole { UserId = legacy.Id, RoleId = adminRole.Id };
        db.AddRange(adminAssignment, new UserRole { UserId = newUser.Id, RoleId = misleadingRole.Id });
        legacy.PasswordChangedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        var adminSignIn = await auth.LoginAsync(new() { Identifier = legacy.Code, Password = "OwnPass123" });
        Check(adminSignIn.IsSuccess && !adminSignIn.Data!.RequiresPasswordChange, "database administrator can log in normally");
        var adminClaims = new JwtSecurityTokenHandler().ReadJwtToken(adminSignIn.Data!.Token).Claims
            .Where(x => x.Type == ClaimTypes.Role || x.Type == "role").Select(x => x.Value).ToList();
        Check(adminClaims.Contains("Admin") && adminClaims.Contains("ADMIN"), "token includes both case-distinct administrator role name and code");
        async Task<IActionResult?> AuthorizeAdmin(long? id, string? roleName, bool authenticated = true)
        {
            var claims = new List<Claim>();
            if (id.HasValue) claims.Add(new(ClaimTypes.NameIdentifier, id.Value.ToString()));
            if (roleName != null) claims.Add(new(ClaimTypes.Role, roleName));
            var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticated ? "test" : null)) };
            var filter = new AuthorizationFilterContext(new ActionContext(http, new RouteData(), new ActionDescriptor(), new ModelStateDictionary()), []);
            await new AdminRoleAuthorizationFilter(db).OnAuthorizationAsync(filter);
            return filter.Result;
        }
        Check(await AuthorizeAdmin(legacy.Id, "SuperAdmin") is null, "legacy admin role name is accepted using current database role code");
        Check(await AuthorizeAdmin(legacy.Id, "Admin") is null, "legacy Admin claim with different casing is accepted");
        Check(await AuthorizeAdmin(legacy.Id, "ADMIN") is null, "current admin token is accepted");
        Check(await AuthorizeAdmin(newUser.Id, "ADMIN") is ForbidResult, "cached ADMIN claim cannot authorize non-admin database role");
        Check(await AuthorizeAdmin(newUser.Id, "SuperAdmin") is ForbidResult, "matching admin role name without ADMIN code does not grant access");
        Check(await AuthorizeAdmin(null, "ADMIN") is ForbidResult, "missing user identity is forbidden");
        Check(await AuthorizeAdmin(legacy.Id, "ADMIN", authenticated: false) is UnauthorizedResult, "anonymous request remains unauthorized");
        adminRole.IsDeleted = true;
        await db.SaveChangesAsync();
        Check(await AuthorizeAdmin(legacy.Id, "ADMIN") is ForbidResult, "deleted administrator role cannot grant access");
        adminRole.IsDeleted = false;
        db.Remove(adminAssignment);
        await db.SaveChangesAsync();
        Check(await AuthorizeAdmin(legacy.Id, "ADMIN") is ForbidResult, "removed administrator assignment immediately revokes access");
        var requestEndpoint = typeof(WebAPI.Controllers.UsersController).GetMethod("RequestPasswordChange")!;
        Check(requestEndpoint.GetCustomAttribute<AdminRoleAuthorizeAttribute>() != null && requestEndpoint.GetCustomAttribute<MenuAuthorizeAttribute>() != null,
            "request endpoint enforces both administrator role and menu edit permission");

        // Generate SQL and compare the committed snapshot entirely offline; this connection is never opened.
        await using var sqlModel = new AppDataContext(new DbContextOptionsBuilder<AppDataContext>()
            .UseSqlServer("Server=offline.invalid;Database=NeverConnect;Integrated Security=true;TrustServerCertificate=true").Options);
        Check(!sqlModel.Database.HasPendingModelChanges(), "migration snapshot matches complete SQL Server model");
        var script = sqlModel.GetService<IMigrator>().GenerateScript("20261005192319_AddMgsSheets", "20261007200000_AddPasswordPolicy", MigrationsSqlGenerationOptions.Idempotent);
        Check(script.Contains("MustChangePassword") && script.Contains("PasswordChangedAt") && script.Contains("PasswordVersion"), "offline migration creates all policy columns");
        Console.WriteLine($"PASS: {checks} password policy checks (in-memory SQLite, fake notifier, zero SMTP and zero remote DB connections).");
    }
}

public class MenuProxy : DispatchProxy
{
    protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name == "GetByUserIdAsync"
        ? Task.FromResult<IReadOnlyList<MenuWithPermissionsDto>>([]) : throw new NotSupportedException(method?.Name);
}
internal sealed class SettingsSnapshot(AppSettings value) : IOptionsSnapshot<AppSettings>
{
    public AppSettings Value => value;
    public AppSettings Get(string? name) => value;
}
internal sealed class TestNotifier : IPasswordPolicyNotifier
{
    public int Calls;
    public long LastId;
    public Task NotifyAsync(long userId, CancellationToken ct = default) { Calls++; LastId = userId; return Task.CompletedTask; }
}
internal sealed class ForbiddenMail : IMailService
{
    public int Calls;
    public Task<ResponseModel<bool>> SendResetPassMailAsync(string bodyMesage, string to) { Calls++; throw new InvalidOperationException("Unexpected mail"); }
    public Task<ResponseModel<bool>> SendLocationOverrideMailAsync(List<string> managers, string subject, string html) { Calls++; throw new InvalidOperationException("Unexpected mail"); }
    public Task SendWithAttachmentAsync(IReadOnlyCollection<string> recipients, string subject, string htmlBody, MailAttachmentData attachment, CancellationToken cancellationToken = default) { Calls++; throw new InvalidOperationException("Unexpected mail"); }
}
internal sealed class PolicyTestContext(DbContextOptions<AppDataContext> options) : AppDataContext(options)
{
    protected override void OnModelCreating(ModelBuilder model)
    {
        var keep = new[] { typeof(User), typeof(UserRole), typeof(Role), typeof(Tenant), typeof(Configuration) };
        foreach (var property in typeof(AppDataContext).GetProperties())
            if (property.PropertyType.IsGenericType && property.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
            {
                var type = property.PropertyType.GetGenericArguments()[0];
                if (!keep.Contains(type)) model.Ignore(type);
            }
        foreach (var type in keep)
        {
            var entity = model.Entity(type);
            foreach (var property in type.GetProperties())
            {
                var target = property.PropertyType;
                if (target.IsGenericType && typeof(System.Collections.IEnumerable).IsAssignableFrom(target)) target = target.GetGenericArguments()[0];
                if (typeof(BaseEntity).IsAssignableFrom(target) && !keep.Contains(target)) entity.Ignore(property.Name);
            }
        }
    }
}
