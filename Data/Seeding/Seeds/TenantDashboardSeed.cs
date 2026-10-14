using Data.Seeding.Abstractions;
using Data.Seeding.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Model.Concrete;
using Model.Dtos.Dashboard;

namespace Data.Seeding.Seeds;

public sealed class TenantDashboardSeed : IDataSeed
{
    public string Key => "tenant.dashboard.permissions.v1";
    public int Order => 110; // After the EKB module.
    public Task<bool> ShouldRunAsync(DbContext db, CancellationToken ct) => Task.FromResult(true);

    public async Task RunAsync(DbContext db, IServiceProvider sp, CancellationToken ct)
    {
        var definitions = new Dictionary<string, string>
        {
            [DashboardMenuKeys.Service] = "Bireysel - Dashboard",
            [DashboardMenuKeys.Ykb] = "YKB - Dashboard",
            [DashboardMenuKeys.Ekb] = "Emlak Katılım - Dashboard",
            [DashboardMenuKeys.Qnb] = "QNB - Dashboard"
        };
        var menus = await db.Set<Menu>().ToListAsync(ct);
        foreach (var definition in definitions)
        {
            if (menus.Any(m => m.Name == definition.Key)) continue;
            var menu = new Menu { Name = definition.Key, Description = definition.Value };
            menus.Add(menu);
            db.Add(menu);
        }
        await db.SaveChangesAsync(ct);
        // One-time legacy grant migration. Repeated starts must not restore a revoked permission.
        if (await db.Set<SeedHistory>().AnyAsync(h => h.Key == Key, ct)) return;
        var roles = await db.Set<Role>().Where(r => !r.IsDeleted).Include(r => r.MenuRoles).ToListAsync(ct);
        var legacyMenuId = menus.FirstOrDefault(m => m.Name == "Dashboard")?.Id;
        foreach (var role in roles)
        {
            if (!role.MenuRoles.Any(mr => mr.MenuId == legacyMenuId && mr.HasView)) continue;
            // Dashboard already allowed these three APIs. Preserve that access explicitly.
            var grants = new List<string> { DashboardMenuKeys.Service, DashboardMenuKeys.Ykb, DashboardMenuKeys.Ekb };
            if (string.Equals(role.Code, "ADMIN", StringComparison.OrdinalIgnoreCase)) grants.Add(DashboardMenuKeys.Qnb);
            foreach (var key in grants)
            {
                var id = menus.Single(m => m.Name == key).Id;
                if (!role.MenuRoles.Any(mr => mr.MenuId == id))
                    role.MenuRoles.Add(new MenuRole { MenuId = id, HasView = true, HasEdit = false });
            }
        }
        db.Add(new SeedHistory { Key = Key, Version = "1", AppliedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(ct);
    }
}
