using System.Text.Json;
using Data.Concrete.EfCore.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Model.Concrete.Collections;

internal static class CollectionMigrationReconciler
{
    public static async Task RunAsync(string sourceSystem, string snapshotKey, byte[] expectedManifestHash,
        string settingsPath)
    {
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath), new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        });
        var connection = new SqlConnectionStringBuilder(settings.RootElement.GetProperty("AppSettings")
            .GetProperty("MSSQLConnectionString").GetString());
        if (connection.DataSource != "192.168.1.8" || connection.InitialCatalog != "AssistFlowTest")
            throw new InvalidOperationException("Aktarım mutabakatı yalnız AssistFlowTest üzerinde çalışır.");
        var options = new DbContextOptionsBuilder<AppDataContext>().UseSqlServer(connection.ConnectionString,
            sql => { sql.EnableRetryOnFailure(); sql.CommandTimeout(180); }).Options;
        await using var db = new AppDataContext(options);
        var batch = await db.Set<CollectionMigrationBatch>().AsNoTracking().SingleAsync(x =>
            x.SourceSystem == sourceSystem && x.SnapshotKey == snapshotKey);
        if (!batch.ManifestHash.SequenceEqual(expectedManifestHash))
            throw new InvalidOperationException("Mutabakat kesiti manifest ile uyuşmuyor.");

        var contractMaps = await db.Set<CollectionMigrationMap>().AsNoTracking()
            .Where(x => x.FirstBatchId == batch.Id && x.EntityCode == "Contract").ToListAsync();
        var rateMaps = await db.Set<CollectionMigrationMap>().AsNoTracking()
            .Where(x => x.FirstBatchId == batch.Id && x.EntityCode == "ContractHistory").ToListAsync();
        if (contractMaps.Any(x => !x.TargetContractId.HasValue) || rateMaps.Any(x => !x.TargetRatePeriodId.HasValue))
            throw new InvalidOperationException("Hedef kimliği eksik migration map bulundu.");
        var contractTargetIds = contractMaps.Select(x => x.TargetContractId!.Value).ToHashSet();
        var rateTargetIds = rateMaps.Select(x => x.TargetRatePeriodId!.Value).ToHashSet();
        var targetContracts = await db.Set<CollectionContract>().AsNoTracking()
            .Where(x => contractTargetIds.Contains(x.Id) && !x.IsDeleted).CountAsync();
        var targetRates = await db.Set<CollectionContractRatePeriod>().AsNoTracking()
            .Where(x => rateTargetIds.Contains(x.Id) && !x.IsDeleted).CountAsync();
        var appliedContracts = await db.Set<CollectionMigrationContractStage>().AsNoTracking()
            .CountAsync(x => x.SourceRow.BatchId == batch.Id && x.Status == CollectionMigrationRowStatus.Applied);
        var appliedRates = await db.Set<CollectionMigrationRatePeriodStage>().AsNoTracking()
            .CountAsync(x => x.SourceRow.BatchId == batch.Id && x.Status == CollectionMigrationRowStatus.Applied);
        if (contractMaps.Count != targetContracts || contractMaps.Count != appliedContracts
            || rateMaps.Count != targetRates || rateMaps.Count != appliedRates)
            throw new InvalidOperationException("Migration map, staging ve hedef adetleri uyuşmuyor.");
        var orphanRates = await db.Set<CollectionContractRatePeriod>().AsNoTracking()
            .CountAsync(x => rateTargetIds.Contains(x.Id) && !contractTargetIds.Contains(x.ContractId));
        if (orphanRates != 0) throw new InvalidOperationException("Aktarılan sözleşmesine bağlanmayan tarife dönemi bulundu.");

        Console.WriteLine($"Mutabakat başarılı: sözleşme map/stage/hedef={contractMaps.Count}; tarife map/stage/hedef={rateMaps.Count}.");
        Console.WriteLine("Hedef tarife mutabakatı (para birimi | davranış | satır | tutar toplamı):");
        var totals = await db.Set<CollectionContractRatePeriod>().AsNoTracking()
            .Where(x => rateTargetIds.Contains(x.Id) && !x.IsDeleted)
            .GroupBy(x => new { x.CurrencyTypeId, x.BillingBehavior })
            .Select(x => new { x.Key.CurrencyTypeId, x.Key.BillingBehavior, Count = x.Count(), Amount = x.Sum(y => y.Amount) })
            .OrderBy(x => x.CurrencyTypeId).ThenBy(x => x.BillingBehavior).ToListAsync();
        foreach (var row in totals)
            Console.WriteLine($"  {row.CurrencyTypeId}|{row.BillingBehavior}|{row.Count}|{row.Amount:0.00}");
        Console.WriteLine("Ödeme ve dosya mutabakatı bu dilimin kapsamı dışındadır; bu aktarım bunları oluşturmadı.");
    }
}
