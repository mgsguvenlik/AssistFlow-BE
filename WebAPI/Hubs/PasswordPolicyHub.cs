using System.Security.Claims;
using Business.Interfaces;
using Core.Utilities.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace WebAPI.Hubs;

[Authorize]
public sealed class PasswordPolicyHub : Hub { }

public sealed class PasswordPolicyNotifier(IHubContext<PasswordPolicyHub> hub, ILogger<PasswordPolicyNotifier> logger) : IPasswordPolicyNotifier
{
    public async Task NotifyAsync(long userId, CancellationToken ct = default)
    {
        try { await hub.Clients.User(userId.ToString()).SendAsync("PasswordChangeRequired", cancellationToken: ct); }
        catch (Exception ex) { logger.LogWarning(ex, "Password policy notification failed for user {UserId}", userId); }
    }
}

public sealed class PasswordPolicyHubFilter(IPasswordPolicyService policy) : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var user = invocationContext.Context.User;
        var id = user?.FindFirstValue(ClaimTypes.NameIdentifier);
        var state = long.TryParse(id, out var userId) ? await policy.GetAsync(userId, invocationContext.Context.ConnectionAborted) : null;
        var raw = user?.FindFirstValue(PasswordPolicyRules.VersionClaim);
        var version = raw is null ? 0 : int.TryParse(raw, out var parsed) ? parsed : -1;
        if (state is null || state.Version != version || state.RequiresChange)
        {
            invocationContext.Context.Abort();
            throw new HubException("Oturumunuz sona erdi veya şifrenizi değiştirmeniz gerekiyor.");
        }
        return await next(invocationContext);
    }
}
