using Data.Seeding.Abstractions;
using Microsoft.EntityFrameworkCore;
using Model.Concrete;

namespace Data.Seeding.Seeds;

public sealed class SheetsMenuSeed : IDataSeed
{
    private static readonly Dictionary<string, string> Menus = new()
    {
        ["SheetsList"] = "MGS Tablolar - Tablolarım",
        ["SheetsCreate"] = "MGS Tablolar - Tablo Oluştur",
        ["SheetsManage"] = "MGS Tablolar - Tüm Tabloları Yönet"
    };
    public string Key => "SeedSheetsMenus";
    public int Order => 32;

    public async Task<bool> ShouldRunAsync(DbContext db, CancellationToken ct) =>
        await db.Set<Menu>().CountAsync(x => Menus.Keys.Contains(x.Name), ct) != Menus.Count;

    public async Task RunAsync(DbContext db, IServiceProvider sp, CancellationToken ct)
    {
        foreach (var item in Menus)
            if (!await db.Set<Menu>().AnyAsync(x => x.Name == item.Key, ct))
                db.Set<Menu>().Add(new Menu { Name = item.Key, Description = item.Value });
        await db.SaveChangesAsync(ct);
    }
}
