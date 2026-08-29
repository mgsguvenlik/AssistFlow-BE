using Data.Seeding.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Model.Concrete;

namespace Data.Seeding.Seeds;

public sealed class HelpdeskSeed(ILogger<HelpdeskSeed> logger) : IDataSeed
{
    private static readonly (string Code, string Name)[] Roles =
    [
        ("HELPDESK_MANAGER", "Helpdesk Yöneticisi"),
        ("HELPDESK_TEAM_LEAD", "Helpdesk Ekip Lideri"),
        ("HELPDESK_AGENT", "Helpdesk Personel")
    ];

    private static readonly (string Name, string Description)[] Menus =
    [
        ("HelpdeskTicketList", "Helpdesk Talep Listesi"),
        ("HelpdeskTicketCreate", "Helpdesk Talebi Oluştur"),
        ("HelpdeskMailbox", "Helpdesk Mailbox Yönetimi"),
        ("HelpdeskMailRules", "Helpdesk Mail Kuralı Yönetimi"),
        ("HelpdeskTicketArchive", "Helpdesk Arşivlenmiş Talep Listesi")
    ];

    public string Key => "SeedHelpdeskRolesAndMenus";
    public int Order => 35;

    public async Task RunAsync(DbContext db, IServiceProvider sp, CancellationToken ct)
    {
        var roleSet = db.Set<Role>();
        foreach (var definition in Roles)
        {
            var role = await roleSet.FirstOrDefaultAsync(x => x.Code != null && x.Code.ToUpper() == definition.Code, ct);
            if (role is null)
            {
                role = new Role { Code = definition.Code, Name = definition.Name, CreatedDate = DateTimeOffset.Now };
                roleSet.Add(role);
            }
            else
            {
                role.Name = definition.Name;
                role.IsDeleted = false;
                role.UpdatedDate = DateTimeOffset.Now;
            }
        }
        await db.SaveChangesAsync(ct);

        var menuSet = db.Set<Menu>();
        foreach (var definition in Menus)
        {
            var menu = await menuSet.FirstOrDefaultAsync(x => x.Name == definition.Name, ct);
            if (menu is null)
            {
                menu = new Menu { Name = definition.Name, Description = definition.Description };
                menuSet.Add(menu);
            }
            else menu.Description = definition.Description;
        }
        await db.SaveChangesAsync(ct);

        var roles = await roleSet.Where(x => !x.IsDeleted && x.Code != null &&
                (x.Code == "ADMIN" || Roles.Select(r => r.Code).Contains(x.Code)))
            .ToDictionaryAsync(x => x.Code!, ct);
        var menus = await menuSet.Where(x => Menus.Select(m => m.Name).Contains(x.Name)).ToDictionaryAsync(x => x.Name, ct);

        await EnsurePermission(db, roles, menus, "ADMIN", "HelpdeskTicketList", true, true, ct);
        await EnsurePermission(db, roles, menus, "ADMIN", "HelpdeskTicketCreate", true, true, ct);
        await EnsurePermission(db, roles, menus, "ADMIN", "HelpdeskMailbox", true, true, ct);
        await EnsurePermission(db, roles, menus, "ADMIN", "HelpdeskMailRules", true, true, ct);
        await EnsurePermission(db, roles, menus, "HELPDESK_MANAGER", "HelpdeskTicketList", true, true, ct);
        await EnsurePermission(db, roles, menus, "HELPDESK_MANAGER", "HelpdeskTicketCreate", true, true, ct);
        await EnsurePermission(db, roles, menus, "HELPDESK_MANAGER", "HelpdeskMailbox", true, true, ct);
        await EnsurePermission(db, roles, menus, "HELPDESK_MANAGER", "HelpdeskMailRules", true, true, ct);
        await EnsurePermission(db, roles, menus, "HELPDESK_TEAM_LEAD", "HelpdeskTicketList", true, true, ct);
        await EnsurePermission(db, roles, menus, "HELPDESK_TEAM_LEAD", "HelpdeskTicketCreate", true, true, ct);
        await EnsurePermission(db, roles, menus, "HELPDESK_TEAM_LEAD", "HelpdeskMailbox", true, true, ct);
        await EnsurePermission(db, roles, menus, "HELPDESK_TEAM_LEAD", "HelpdeskMailRules", true, true, ct);
        await EnsurePermission(db, roles, menus, "HELPDESK_AGENT", "HelpdeskTicketList", true, true, ct);
        await EnsurePermission(db, roles, menus, "HELPDESK_MANAGER", "HelpdeskTicketArchive", true, true, ct);
        await EnsurePermission(db, roles, menus, "HELPDESK_TEAM_LEAD", "HelpdeskTicketArchive", true, true, ct);
        await EnsurePermission(db, roles, menus, "HELPDESK_AGENT", "HelpdeskTicketArchive", true, false, ct);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Helpdesk rol, menü ve izin seed'i doğrulandı.");
    }

    public Task<bool> ShouldRunAsync(DbContext db, CancellationToken ct) => Task.FromResult(true);

    private static async Task EnsurePermission(DbContext db, IReadOnlyDictionary<string, Role> roles,
        IReadOnlyDictionary<string, Menu> menus, string roleCode, string menuName, bool view, bool edit, CancellationToken ct)
    {
        if (!roles.TryGetValue(roleCode, out var role) || !menus.TryGetValue(menuName, out var menu)) return;
        var permission = await db.Set<MenuRole>().FirstOrDefaultAsync(x => x.RoleId == role.Id && x.MenuId == menu.Id, ct);
        if (permission is null)
            db.Set<MenuRole>().Add(new MenuRole { RoleId = role.Id, MenuId = menu.Id, HasView = view, HasEdit = edit });
        else
        {
            permission.HasView = view;
            permission.HasEdit = edit;
        }
    }
}
