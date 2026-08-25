using Data.Seeding.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Model.Concrete;

namespace Data.Seeding.Seeds
{
    public sealed class PeriodicReportMenuSeed : IDataSeed
    {
        public const string MenuName = "PeriodicReportList";
        private readonly ILogger<PeriodicReportMenuSeed> _logger;

        public PeriodicReportMenuSeed(ILogger<PeriodicReportMenuSeed> logger) => _logger = logger;

        public string Key => "SeedPeriodicReportMenu";
        public int Order => 30;

        public async Task RunAsync(DbContext db, IServiceProvider sp, CancellationToken ct)
        {
            var menu = await db.Set<Menu>().FirstOrDefaultAsync(x => x.Name == MenuName, ct);
            if (menu == null)
            {
                menu = new Menu { Name = MenuName, Description = "Periyodik Raporlar" };
                db.Set<Menu>().Add(menu);
                await db.SaveChangesAsync(ct);
            }

            var adminRoleIds = await db.Set<Role>()
                .Where(x => !x.IsDeleted &&
                    ((x.Code != null && x.Code.ToUpper() == "ADMIN") || x.Name.ToUpper() == "ADMIN"))
                .Select(x => x.Id)
                .ToListAsync(ct);

            foreach (var roleId in adminRoleIds)
            {
                var existing = await db.Set<MenuRole>()
                    .FirstOrDefaultAsync(x => x.MenuId == menu.Id && x.RoleId == roleId, ct);
                if (existing == null)
                {
                    db.Set<MenuRole>().Add(new MenuRole
                    {
                        MenuId = menu.Id,
                        RoleId = roleId,
                        HasView = true,
                        HasEdit = true
                    });
                }
                else
                {
                    existing.HasView = true;
                    existing.HasEdit = true;
                }
            }

            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Periyodik rapor menüsü ve Admin rol yetkisi doğrulandı.");
        }

        public async Task<bool> ShouldRunAsync(DbContext db, CancellationToken ct)
        {
            var menuId = await db.Set<Menu>()
                .Where(x => x.Name == MenuName)
                .Select(x => (long?)x.Id)
                .FirstOrDefaultAsync(ct);
            if (!menuId.HasValue)
                return true;

            var adminRoleIds = db.Set<Role>()
                .Where(x => !x.IsDeleted &&
                    ((x.Code != null && x.Code.ToUpper() == "ADMIN") || x.Name.ToUpper() == "ADMIN"))
                .Select(x => x.Id);

            return !await db.Set<MenuRole>().AnyAsync(x =>
                x.MenuId == menuId.Value &&
                adminRoleIds.Contains(x.RoleId) &&
                x.HasView && x.HasEdit, ct);
        }
    }
}
