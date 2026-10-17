using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Business.Services.Crm.Collections;
using Data.Concrete.EfCore.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Model.Concrete.Collections;

internal static class CollectionBankSetup
{
    public static async Task RunAsync(string settingsPath, string? action)
    {
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var cs = new SqlConnectionStringBuilder(settings.RootElement.GetProperty("AppSettings").GetProperty("MSSQLConnectionString").GetString());
        if (cs.DataSource != "192.168.1.8" || cs.InitialCatalog != "AssistFlowTest") throw new InvalidOperationException("Yalnız AssistFlowTest hedefi kullanılabilir.");
        await using var db = new AppDataContext(new DbContextOptionsBuilder<AppDataContext>().UseSqlServer(cs.ConnectionString, o => o.CommandTimeout(120)).Options);
        if (db.Database.HasPendingModelChanges()) throw new InvalidOperationException("Model/snapshot farklı.");
        if (action == "--install")
        {
            if ((await db.Database.GetPendingMigrationsAsync()).Any(x => !x.EndsWith("_AddCollectionBankLoads") && !x.EndsWith("_AddCollectionBankReferenceIndexes"))) throw new InvalidOperationException("Yalnız K08 migrationları uygulanabilir.");
            await db.Database.MigrateAsync(); Console.WriteLine("K08 test şeması hazır."); return;
        }
        // Every historical bank identity is retained, including rejected/excluded subscriptions.
        // This conservative guard also protects source payments deliberately NOT migrated to AssistFlow.
        var source = await db.Database.SqlQueryRaw<SourceRow>("SELECT CAST(BulkPaymentID AS bigint) Id, FileType, FileRowID FROM MGS.Core.BulkPayment ORDER BY BulkPaymentID").ToListAsync();
        if (source.Any(x => x.FileType is not ("GTS" or "IVR"))) throw new InvalidOperationException("Kaynakta bilinmeyen banka türü var.");
        var keys = source.Where(x => !string.IsNullOrWhiteSpace(x.FileRowID)).Select(x => CollectionBankLoadService.Key(x.FileType, x.FileRowID!)).Distinct().Order().ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(source)));
        Console.WriteLine(JsonSerializer.Serialize(new { Hash = hash, SourceRows = source.Count, UniqueKeys = keys.Length, BlankKeys = source.Count(x => string.IsNullOrWhiteSpace(x.FileRowID)) }));
        if (action is null) return;
        if (action != hash) throw new InvalidOperationException("Kaynak değişti; hash eşleşmedi.");
        // Blank historical IDs would make a complete duplicate guard impossible.
        if (source.Any(x => string.IsNullOrWhiteSpace(x.FileRowID))) throw new InvalidOperationException("Boş banka kimlikleri var; geçmiş koruması kurulamadı.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var existing = (await db.Set<CollectionBankTransaction>().Select(x => x.Key).ToListAsync()).ToHashSet();
        using var table = new DataTable();
        table.Columns.Add("Key", typeof(string)); table.Columns.Add("Source", typeof(string)); table.Columns.Add("RecordedAt", typeof(DateTimeOffset));
        foreach (var key in keys.Where(x => !existing.Contains(x))) table.Rows.Add(key, "MGS", DateTimeOffset.UtcNow);
        using var bulk = new SqlBulkCopy((SqlConnection)db.Database.GetDbConnection(), SqlBulkCopyOptions.CheckConstraints, (SqlTransaction)tx.GetDbTransaction()) { DestinationTableName = "collection.BankTransaction", BulkCopyTimeout = 120 };
        foreach (DataColumn c in table.Columns) bulk.ColumnMappings.Add(c.ColumnName, c.ColumnName);
        await bulk.WriteToServerAsync(table);
        var baseline = await db.Set<CollectionBankBaseline>().SingleOrDefaultAsync(x => x.Id == 1);
        if (baseline is null) { baseline = new() { Id = 1 }; db.Add(baseline); }
        baseline.SourceHash = hash; baseline.SourceCount = source.Count; baseline.VerifiedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(); await tx.CommitAsync();
        Console.WriteLine($"Geçmiş koruması kuruldu: {table.Rows.Count} yeni kimlik. MGS ve canlı ortam değiştirilmedi.");
    }
    public sealed class SourceRow { public long Id { get; set; } public string FileType { get; set; } = ""; public string? FileRowID { get; set; } }
}
