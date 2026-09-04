using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Authorization;

/// <summary>Requires the current user to have the specified permission on at least one menu key.</summary>
public sealed class MenuAuthorizeAttribute : TypeFilterAttribute
{
    public MenuAuthorizeAttribute(MenuPermission permission)
        : base(typeof(MenuAuthorizationFilter))
    {
        Arguments = new object[] { Array.Empty<string>(), permission };
    }

    public MenuAuthorizeAttribute(string menuKey, MenuPermission permission)
        : this(new[] { menuKey }, permission)
    {
    }

    public MenuAuthorizeAttribute(string[] menuKeys, MenuPermission permission)
        : base(typeof(MenuAuthorizationFilter))
    {
        Arguments = new object[] { menuKeys, permission };
    }
}
