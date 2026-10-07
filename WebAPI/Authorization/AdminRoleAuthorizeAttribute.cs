using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Authorization;

/// <summary>Requires the current user's active ADMIN role in the database.</summary>
public sealed class AdminRoleAuthorizeAttribute : TypeFilterAttribute
{
    public AdminRoleAuthorizeAttribute() : base(typeof(AdminRoleAuthorizationFilter)) { }
}
