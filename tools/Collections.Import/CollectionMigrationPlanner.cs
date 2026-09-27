using System.Text.Json;
using Data.Concrete.EfCore.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Model.Concrete.Collections;

internal static class CollectionMigrationPlanner
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
            throw new InvalidOperationException("Aktarım planı yalnız AssistFlowTest üzerinde çalışır.");
        var options = new DbContextOptionsBuilder<AppDataContext>().UseSqlServer(connection.ConnectionString, sql =>
        {
            sql.EnableRetryOnFailure();
            sql.CommandTimeout(180);
        }).Options;
        await using var db = new AppDataContext(options);
        var batch = await db.Set<CollectionMigrationBatch>().AsNoTracking().SingleOrDefaultAsync(x =>
            x.SourceSystem == sourceSystem && x.SnapshotKey == snapshotKey)
            ?? throw new InvalidOperationException("Kesit staging kaydı bulunamadı.");
        if (!batch.ManifestHash.SequenceEqual(expectedManifestHash))
            throw new InvalidOperationException("Staging manifest hash'i kesitle uyuşmuyor.");
        if (batch.Status != CollectionMigrationBatchStatus.NeedsReview)
            throw new InvalidOperationException("Aktarım planı yalnız doğrulanmış ve inceleme bekleyen kesitten üretilebilir.");

        var plan = await CollectionTransferPlan.LoadAsync(db, batch, itemTwoOnly: false);
        Console.WriteLine("DRY-RUN; hiçbir hedef sözleşme, tarife veya ödeme kaydı oluşturulmadı.");
        plan.Print();
        Console.WriteLine("Tam kümeye giremeyen sözleşmeler yüklenmez; staging durumu değiştirilmedi.");
    }
}
