using System.Security.Claims;
using Data.Concrete.EfCore.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace WebAPI.Authorization;

public sealed class AdminRoleAuthorizationFilter(AppDataContext db) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var principal = context.HttpContext.User;
        if (principal.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }
        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
        if (!long.TryParse(id, out var userId) || userId <= 0)
        {
            context.Result = new ForbidResult();
            return;
        }

        // Legacy tokens may carry only the role name (SuperAdmin), not its ADMIN code.
        // Read the current assignment so role removal also takes effect without a new login.
        var isAdmin = await (
            from assignment in db.UserRoles.AsNoTracking()
            join role in db.Roles.AsNoTracking() on assignment.RoleId equals role.Id
            join user in db.Users.AsNoTracking() on assignment.UserId equals user.Id
            where assignment.UserId == userId && user.IsActive && !user.IsDeleted
                  && !role.IsDeleted && role.Code != null && role.Code.ToUpper() == "ADMIN"
            select assignment.Id).AnyAsync(context.HttpContext.RequestAborted);

        if (!isAdmin) context.Result = new ForbidResult();
    }
}
