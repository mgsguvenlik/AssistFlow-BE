namespace WebAPI.Authorization;

/// <summary>Declares the menu key that protects a controller's default CRUD actions.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class MenuResourceAttribute : Attribute
{
    public MenuResourceAttribute(string menuKey) => MenuKey = menuKey;

    public string MenuKey { get; }
}
