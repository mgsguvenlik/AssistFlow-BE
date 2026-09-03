namespace WebAPI.Authorization;

/// <summary>Declares the menu key that protects a controller's default CRUD actions.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class MenuResourceAttribute : Attribute
{
    public MenuResourceAttribute(string menuKey, params string[] lookupMenuKeys)
    {
        MenuKey = menuKey;
        LookupMenuKeys = lookupMenuKeys;
    }

    public string MenuKey { get; }
    public IReadOnlyCollection<string> LookupMenuKeys { get; }

    /// <summary>
    /// Allows the controller's read-only lookup actions to be consumed by any
    /// service-request workflow screen. Mutation actions still require MenuKey.
    /// </summary>
    public bool AllowWorkflowRead { get; set; }
}
