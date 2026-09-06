using System.Security.Claims;
using Data.Concrete.EfCore.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.EntityFrameworkCore;

namespace WebAPI.Authorization;

public sealed class MenuAuthorizationFilter : IAsyncAuthorizationFilter
{
    private static readonly string[] WorkflowReadMenuKeys =
    {
        "ServiceRequestCreate",
        "ServiceRequestList",
        "ServiceRequestArchive",
        "ServiceRequestWarehouse",
        "ServiceRequestTechnicalService",
        "ServiceRequestPricing",
        "ServiceRequestFinalApproval",
        "TechnicianDashboard",

        "YkbCustomerServiceRequestCreate",
        "YkbServiceRequestCreate",
        "YkbServiceRequestList",
        "YkbServiceRequestArchive",
        "YkbServiceRequestWarehouse",
        "YkbServiceRequestTechnicalService",
        "YkbServiceRequestPricing",
        "YkbServiceRequestFinalApproval",
        "YkbServiceRequestCustomerAgreement",
        "YkbTechnicianDashboard",
        
        "EkbCustomerServiceRequestCreate",
        "EkbServiceRequestCreate",
        "EkbServiceRequestList",
        "EkbServiceRequestArchive",
        "EkbServiceRequestWarehouse",
        "EkbServiceRequestTechnicalService",
        "EkbServiceRequestPricing",
        "EkbServiceRequestFinalApproval",
        "EkbServiceRequestCustomerAgreement",
        "EkbTechnicianDashboard",


        "QnbCustomerServiceRequestCreate",
        "QnbServiceRequestCreate",
        "QnbServiceRequestList",
        "QnbServiceRequestArchive",
        "QnbServiceRequestWarehouse",
        "QnbServiceRequestTechnicalService",
        "QnbServiceRequestPricing",
        "QnbServiceRequestFinalApproval",
        "QnbServiceRequestCustomerAgreement",
        "QnbTechnicianDashboard"
    };

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
            if (resource is null)
            {
                menuKeys = Array.Empty<string>();
            }
            else if (_permission == MenuPermission.Edit)
            {
                menuKeys = new[] { resource.MenuKey };
            }
            else
            {
                menuKeys = new[] { resource.MenuKey }
                    .Concat(resource.LookupMenuKeys)
                    .Concat(resource.AllowWorkflowRead ? WorkflowReadMenuKeys : Array.Empty<string>())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
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
