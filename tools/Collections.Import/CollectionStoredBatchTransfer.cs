using System.Text.Json;
using Data.Concrete.EfCore.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Model.Concrete.Collections;

internal static class CollectionStoredBatchTransfer
{
    public static async Task RunAsync(long batchId, string settingsPath, string? expectedPlanHash, bool frozenOnly = false, bool itemFourOnly = false, bool itemFiveOnly = false)
    {
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath), new JsonDocumentOptions
        { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        var connection = new SqlConnectionStringBuilder(settings.RootElement.GetProperty("AppSettings").GetProperty("MSSQLConnectionString").GetString());
        if (connection.DataSource != "192.168.1.8" || connection.InitialCatalog != "AssistFlowTest" || batchId <= 0)
            throw new InvalidOperationException("Karar bazlı aktarım yalnız geçerli AssistFlowTest kesitinde yapılır.");
        await using var db = new AppDataContext(new DbContextOptionsBuilder<AppDataContext>()
            .UseSqlServer(connection.ConnectionString, sql => sql.CommandTimeout(180)).Options);
        var batch = await db.Set<CollectionMigrationBatch>().AsNoTracking().SingleAsync(x => x.Id == batchId);
        if (batch.Status != CollectionMigrationBatchStatus.NeedsReview) throw new InvalidOperationException("Kesit inceleme durumunda değil.");
        var plan = await CollectionTransferPlan.LoadAsync(db, batch, itemTwoOnly: !frozenOnly && !itemFourOnly && !itemFiveOnly, frozenOnly: frozenOnly, itemFourOnly: itemFourOnly, itemFiveOnly: itemFiveOnly);
        plan.Print();
        var output = Path.Combine("tools", "Collections.Import", "snapshots", (itemFiveOnly ? "item-five-transfer-" : itemFourOnly ? "item-four-transfer-" : frozenOnly ? "item-one-frozen-" : "item-two-") + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff"));
        Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(Path.Combine(output, "plan.json"), JsonSerializer.Serialize(new
        {
            plan.Hash, plan.Candidates, plan.Frozen, plan.OtherBlocked, plan.FrozenDateReviewIds,
            Contracts = plan.Contracts.Select(x => new { x.SourceRowId, x.SourceRow.SourceId, x.TargetCustomerId }),
            Rates = plan.Rates.Select(x => new { x.SourceRowId, x.SourceContractId, x.Amount, x.TargetCurrencyTypeId, x.BillingBehavior })
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Yerel plan kaydı: {Path.GetFullPath(output)}");
        if (expectedPlanHash is null) { Console.WriteLine("DRY-RUN; veri değiştirilmedi."); return; }
        if (!plan.Hash.Equals(expectedPlanHash, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Aktarım planı değişmiş.");
        await CollectionMigrationApplier.RunAsync(batch.SourceSystem, batch.SnapshotKey, batch.ManifestHash,
            settingsPath, expectedPlanHash, itemTwoOnly: !frozenOnly && !itemFourOnly && !itemFiveOnly, frozenOnly: frozenOnly, itemFourOnly: itemFourOnly, itemFiveOnly: itemFiveOnly);
        await CollectionMigrationReconciler.RunAsync(batch.SourceSystem, batch.SnapshotKey, batch.ManifestHash, settingsPath);
    }
}
