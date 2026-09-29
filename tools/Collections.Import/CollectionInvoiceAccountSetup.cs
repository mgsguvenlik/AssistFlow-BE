using System.Security.Cryptography;
using System.Text.Json;
using Business.Services.Crm.Collections;
using Data.Concrete.EfCore.Collections;
using Data.Concrete.EfCore.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Model.Concrete.Collections;

internal static class CollectionInvoiceAccountSetup
{
    public static async Task RunAsync(string settingsPath, string output, string? hash)
    {
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var cs = new SqlConnectionStringBuilder(settings.RootElement.GetProperty("AppSettings").GetProperty("MSSQLConnectionString").GetString());
        if (cs.DataSource != "192.168.1.8" || cs.InitialCatalog != "AssistFlowTest") throw new InvalidOperationException("Yalnız AssistFlowTest hedefi kullanılabilir.");
        await using var db = new AppDataContext(new DbContextOptionsBuilder<AppDataContext>().UseSqlServer(cs.ConnectionString, o => o.CommandTimeout(120)).Options);
        if (db.Database.HasPendingModelChanges()) throw new InvalidOperationException("Model ve snapshot farklı.");
        if (hash == "--install")
        {
            var pending = (await db.Database.GetPendingMigrationsAsync()).ToArray();
            if (pending.Any(x => x is not ("20260929100739_AddCollectionInvoiceLoads" or "20260929101445_AddCollectionInvoiceLoadRowAudit"))) throw new InvalidOperationException("Yalnız K07 migrationı uygulanabilir.");
            if (pending.Length > 0) await db.Database.MigrateAsync();
            Console.WriteLine("K07 şeması hazır."); return;
        }
        if ((await db.Database.GetPendingMigrationsAsync()).Any()) throw new InvalidOperationException("Bekleyen migration var.");
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var links = await (from m in db.Set<CollectionMigrationMap>().AsNoTracking()
            join s in db.Set<CollectionMigrationSourceRow>().AsNoTracking() on new { BatchId = m.FirstBatchId, m.EntityCode, m.SourceId } equals new { s.BatchId, s.EntityCode, s.SourceId }
            join stage in db.Set<CollectionMigrationContractStage>().AsNoTracking() on s.Id equals stage.SourceRowId
            join c in db.Set<CollectionContract>().AsNoTracking() on m.TargetContractId equals (long?)c.Id
            where m.SourceSystem == "MGS" && m.EntityCode == "Contract" && !c.IsDeleted && stage.TargetCustomerId == c.CustomerId
            select new { stage.SourceCustomerId, c.CustomerId }).ToListAsync();
        var pairs = links.Where(x => long.TryParse(x.SourceCustomerId, out _)).Select(x => (Source: long.Parse(x.SourceCustomerId!), Target: x.CustomerId)).ToList();
        pairs.AddRange((await db.Set<CollectionGroupParent>().AsNoTracking().ToListAsync()).Select(x => (x.LegacyCustomerId, x.CustomerId)));
        var map = pairs.GroupBy(x => x.Source).ToDictionary(g => g.Key, g => g.Select(x => x.Target).Distinct().ToArray());
        var scope = (await CollectionCustomerScopeQuery.Customers(db.Customers.AsNoTracking()).Select(x => x.Id).ToListAsync()).ToHashSet();
        var source = await db.Database.SqlQueryRaw<AccountSource>("SELECT CAST(CustomerID AS bigint) Id, AccountNo FROM MGS.Core.Customer WHERE NULLIF(LTRIM(RTRIM(AccountNo)), '') IS NOT NULL ORDER BY CustomerID").ToListAsync();
        var plan = source.GroupBy(x => CollectionInvoiceLoadService.Normalize(x.AccountNo)).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g =>
        {
            var ids = g.Select(x => x.Id).Order().ToArray();
            var targets = ids.SelectMany(x => map.GetValueOrDefault(x) ?? []).Distinct().Order().ToArray();
            var issue = g.Key.Length > 200 ? "Cari kod 200 karakterden uzun." : ids.Length != 1 ? "Cari kod legacy sistemde birden fazla müşteriye ait; otomatik seçim yapılmadı."
                : targets.Length != 1 ? "Korunan müşteri eşlemesi tekil değil veya bulunamadı." : !scope.Contains(targets[0]) ? "Müşteri tahsilat kapsamı dışında." : null;
            return new { Code = g.Key, SourceIds = ids, CustomerId = issue is null ? (long?)targets[0] : null, Issue = issue };
        }).ToArray();
        var planHash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(plan)));
        var summary = new { Codes = plan.Length, Mapped = plan.Count(x => x.CustomerId != null), Issues = plan.Where(x => x.Issue != null).GroupBy(x => x.Issue!).ToDictionary(g => g.Key, g => g.Count()) };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new { PlanHash = planHash, Summary = summary, Rows = plan }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(JsonSerializer.Serialize(new { PlanHash = planHash, Summary = summary }));
        if (hash is null) { Console.WriteLine("Salt okunur cari önizlemesi tamamlandı."); return; }
        if (!string.Equals(hash, planHash, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Cari plan değişti; yazılmadı.");
        var existing = await db.Set<CollectionInvoiceAccount>().ToDictionaryAsync(x => x.Code);
        var activeCodes = plan.Select(x => x.Code).ToHashSet();
        foreach (var old in existing.Values.Where(x => !activeCodes.Contains(x.Code))) { old.CustomerId = null; old.Issue = "Cari kod artık kaynakta bulunmuyor; yeniden inceleyin."; old.VerifiedAt = DateTimeOffset.UtcNow; }
        foreach (var row in plan.Where(x => x.Code.Length <= 200))
        {
            if (!existing.TryGetValue(row.Code, out var entity)) { entity = new CollectionInvoiceAccount { Code = row.Code }; db.Add(entity); }
            entity.CustomerId = row.CustomerId; entity.Issue = row.Issue; entity.SourceIdsJson = JsonSerializer.Serialize(row.SourceIds);
            entity.SourceHash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(row))); entity.VerifiedAt = DateTimeOffset.UtcNow;
        }
        await db.SaveChangesAsync(); await tx.CommitAsync();
        Console.WriteLine("Cari eşleme collection.InvoiceAccount tablosuna kaydedildi; ortak müşteri tabloları değiştirilmedi.");
    }
    public sealed class AccountSource { public long Id { get; set; } public string AccountNo { get; set; } = ""; }
}
