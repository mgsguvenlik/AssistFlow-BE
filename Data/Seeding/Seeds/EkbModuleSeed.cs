using Data.Seeding.Abstractions;
using Microsoft.EntityFrameworkCore;
using Model.Concrete;

namespace Data.Seeding.Seeds;

/// <summary>Creates EKB tenant, menus and YKB-equivalent role definitions without assigning users.</summary>
public sealed class EkbModuleSeed : IDataSeed
{
    public string Key => "ekb.module.v1";
    public int Order => 100;
    public Task<bool> ShouldRunAsync(DbContext db, CancellationToken ct) => Task.FromResult(true);

    private static string Ekb(string value) => value.Replace("YKB", "EKB").Replace("Ykb", "Ekb").Replace("ykb", "ekb");

    public async Task RunAsync(DbContext db, IServiceProvider sp, CancellationToken ct)
    {
        if (!await db.Set<Tenant>().AnyAsync(x => x.Code == "EKB", ct))
            db.Add(new Tenant { Code = "EKB", Name = "Emlak Katılım", IsActive = true, CreatedDate = DateTimeOffset.UtcNow });

        var menus = await db.Set<Menu>().ToListAsync(ct);
        var sourceMenus = menus.Where(x => x.Name.StartsWith("Ykb", StringComparison.OrdinalIgnoreCase)).ToList();
        var required = new[] {
            "EkbCustomerServiceRequestCreate", "EkbServiceRequestCreate", "EkbServiceRequestList",
            "EkbServiceRequestArchive", "EkbServiceRequestWarehouse", "EkbServiceRequestTechnicalService",
            "EkbServiceRequestPricing", "EkbServiceRequestFinalApproval", "EkbServiceRequestCustomerAgreement",
            "EkbServiceReportsList", "EkbBasicWorkflowReportsList", "EkbAccountingServiceReportList",
            "EkbFlowStatusList", "EkbTechnicianDashboard", "EkbOvertimeReport", "EkbDashboard", "EkbTechnicalServiceGuide"
        };
        foreach (var name in required.Concat(sourceMenus.Select(x => Ekb(x.Name))).Distinct())
        {
            if (menus.Any(x => x.Name == name)) continue;
            var sourceMenu = sourceMenus.FirstOrDefault(x => Ekb(x.Name) == name);
            var description = sourceMenu?.Description;
            var menu = new Menu { Name = name, Description = string.IsNullOrWhiteSpace(description)
                ? "Emlak Katılım - " + name
                : "Emlak Katılım - " + description.Replace("YKB", "").Replace("Ykb", "").Trim() };
            menus.Add(menu);
            db.Add(menu);
        }
        await db.SaveChangesAsync(ct);

        var roles = await db.Set<Role>().Include(x => x.MenuRoles).ToListAsync(ct);
        var sourceRoles = roles.Where(x => (x.Code ?? "").Contains("YKB", StringComparison.OrdinalIgnoreCase)
            || x.Name.Contains("YKB", StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var source in sourceRoles)
        {
            var code = source.Code == null ? null : Ekb(source.Code);
            if (code != null && code == source.Code) code = "EKB_" + code;
            var name = Ekb(source.Name);
            var target = roles.FirstOrDefault(x => code != null ? x.Code == code : x.Name == name);
            if (target != null) continue;
            target = new Role { Code = code, Name = name, CreatedDate = DateTimeOffset.UtcNow };
            foreach (var permission in source.MenuRoles)
            {
                var sourceMenu = menus.FirstOrDefault(x => x.Id == permission.MenuId);
                if (sourceMenu == null) continue;
                var targetMenu = menus.FirstOrDefault(x => x.Name == Ekb(sourceMenu.Name));
                if (targetMenu == null) continue;
                if (target.MenuRoles.Any(x => x.MenuId == targetMenu.Id)) continue;
                target.MenuRoles.Add(new MenuRole { MenuId = targetMenu.Id, HasView = permission.HasView, HasEdit = permission.HasEdit });
            }
            roles.Add(target);
            db.Add(target);
        }
        // Preserve administrator access to the newly added module.
        foreach (var admin in roles.Where(x => x.Code == "ADMIN"))
        foreach (var menu in menus.Where(x => x.Name.StartsWith("Ekb")))
            if (!admin.MenuRoles.Any(x => x.MenuId == menu.Id))
                admin.MenuRoles.Add(new MenuRole { MenuId = menu.Id, HasView = true, HasEdit = true });
        await db.SaveChangesAsync(ct);
    }
}
