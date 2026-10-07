using System.Security.Claims;
using Business.Interfaces;
using Core.Utilities.Security;
using Microsoft.AspNetCore.Authorization;

namespace WebAPI.Middleware;

public sealed class PasswordPolicyMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IPasswordPolicyService policy)
    {
        if (context.User.Identity?.IsAuthenticated != true ||
            context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() != null)
        {
            await next(context);
            return;
        }
        var id = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.FindFirstValue("sub");
        var state = long.TryParse(id, out var userId) ? await policy.GetAsync(userId, context.RequestAborted) : null;
        var versionValue = context.User.FindFirstValue(PasswordPolicyRules.VersionClaim);
        // Tokens from before rollout are accepted only while the user's password is still at version zero.
        var version = versionValue is null ? 0 : int.TryParse(versionValue, out var parsed) ? parsed : -1;
        if (state is null || state.Version != version)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { message = "Oturumunuz sona erdi. Lütfen tekrar giriş yapın." });
            return;
        }
        if (state.RequiresChange)
        {
            var token = policy.CreateToken(state.UserId, state.Version, PasswordPolicyRules.ChangePurpose, out var expires);
            context.Response.StatusCode = StatusCodes.Status428PreconditionRequired;
            context.Response.Headers.CacheControl = "no-store";
            await context.Response.WriteAsJsonAsync(new
            {
                code = "PASSWORD_CHANGE_REQUIRED", message = state.Reason,
                passwordChangeToken = token, passwordChangeTokenExpires = expires, passwordChangeReason = state.Reason
            });
            return;
        }
        await next(context);
    }
}
