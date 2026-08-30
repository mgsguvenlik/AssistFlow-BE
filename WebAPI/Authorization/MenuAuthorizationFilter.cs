using System.Security.Claims;
using Data.Concrete.EfCore.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.EntityFrameworkCore;

namespace WebAPI.Authorization;

public sealed class MenuAuthorizationFilter : IAsyncAuthorizationFilter
{
    private readonly AppDataContext _db;
    private readonly IReadOnlyCollection<string> _menuKeys;
    private readonly MenuPermission _permission;

    public MenuAuthorizationFilter(AppDataContext db, string[] menuKeys, MenuPermission permission)
    {
        _db = db;
        _menuKeys = menuKeys.Where(x => !string.IsNullOrWhiteSpace(x))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToArray();
        _permission = permission;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var userIdValue = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? context.HttpContext.User.FindFirstValue("sub");
        var menuKeys = _menuKeys;
        if (menuKeys.Count == 0 && context.ActionDescriptor is ControllerActionDescriptor action)
        {
            var resource = action.ControllerTypeInfo
                .GetCustomAttributes(typeof(MenuResourceAttribute), inherit: true)
                .OfType<MenuResourceAttribute>()
                .SingleOrDefault();
            menuKeys = resource is null ? Array.Empty<string>() : new[] { resource.MenuKey };
        }

        if (!long.TryParse(userIdValue, out var userId) || userId <= 0 || menuKeys.Count == 0)
        {
            context.Result = new ForbidResult();
            return;
        }

        var hasPermission = await (
            from userRole in _db.UserRoles.AsNoTracking()
            join menuRole in _db.MenuRoles.AsNoTracking() on userRole.RoleId equals menuRole.RoleId
            join menu in _db.Menus.AsNoTracking() on menuRole.MenuId equals menu.Id
            where userRole.UserId == userId
                  && menuKeys.Contains(menu.Name)
                  && (_permission == MenuPermission.View ? menuRole.HasView : menuRole.HasEdit)
            select menuRole.Id)
            .AnyAsync(context.HttpContext.RequestAborted);

        if (!hasPermission)
            context.Result = new ForbidResult();
    }
}
