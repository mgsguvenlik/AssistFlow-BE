using System.Text.Json;
using Data.Concrete.EfCore.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Model.Concrete.Collections;
using Model.Concrete;

internal static class CollectionStageSummary
{
    public static async Task RunAsync(string sourceSystem, string snapshotKey, string settingsPath)
    {
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath), new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        });
        var connection = new SqlConnectionStringBuilder(settings.RootElement.GetProperty("AppSettings")
            .GetProperty("MSSQLConnectionString").GetString());
        if (connection.DataSource != "192.168.1.8" || connection.InitialCatalog != "AssistFlowTest")
            throw new InvalidOperationException("Staging özeti yalnız AssistFlowTest üzerinde çalışır.");
        var options = new DbContextOptionsBuilder<AppDataContext>()
            .UseSqlServer(connection.ConnectionString, sql => sql.EnableRetryOnFailure()).Options;
        await using var db = new AppDataContext(options);
        var batch = await db.Set<CollectionMigrationBatch>().AsNoTracking().SingleOrDefaultAsync(x =>
            x.SourceSystem == sourceSystem && x.SnapshotKey == snapshotKey)
            ?? throw new InvalidOperationException("Kesit staging kaydı bulunamadı.");

        var contracts = await db.Set<CollectionMigrationContractStage>().AsNoTracking()
            .Where(x => x.SourceRow.BatchId == batch.Id).GroupBy(x => x.Status)
            .Select(x => new { Status = x.Key, Count = x.Count() }).OrderBy(x => x.Status).ToListAsync();
        var histories = await db.Set<CollectionMigrationRatePeriodStage>().AsNoTracking()
            .Where(x => x.SourceRow.BatchId == batch.Id).GroupBy(x => x.Status)
            .Select(x => new { Status = x.Key, Count = x.Count() }).OrderBy(x => x.Status).ToListAsync();
        var issues = await db.Set<CollectionMigrationIssue>().AsNoTracking()
            .Where(x => x.SourceRow.BatchId == batch.Id && x.ResolvedDate == null)
            .GroupBy(x => x.IssueCode).Select(x => new { Code = x.Key, Count = x.Count() })
            .OrderByDescending(x => x.Count).ThenBy(x => x.Code).ToListAsync();

        Console.WriteLine($"Batch durumu: {batch.Status}");
        Console.WriteLine("Sözleşme staging: " + string.Join(", ", contracts.Select(x => $"{x.Status}={x.Count}")));
        Console.WriteLine("Tarihçe staging: " + string.Join(", ", histories.Select(x => $"{x.Status}={x.Count}")));
        Console.WriteLine("Açık karantina nedenleri:");
        foreach (var issue in issues) Console.WriteLine($"  {issue.Code}: {issue.Count}");

        Console.WriteLine("Hedef referans kataloğu:");
        foreach (var row in await db.Set<CollectionPaymentFrequency>().AsNoTracking().OrderBy(x => x.Id)
                     .Select(x => new { Kind = "PaymentFrequency", x.Id, x.Code, x.Name }).ToListAsync())
            Console.WriteLine($"  {row.Kind}|{row.Id}|{row.Code}|{row.Name}");
        foreach (var row in await db.Set<CollectionContractStatus>().AsNoTracking().OrderBy(x => x.Id)
                     .Select(x => new { Kind = "ContractStatus", x.Id, x.Code, x.Name }).ToListAsync())
            Console.WriteLine($"  {row.Kind}|{row.Id}|{row.Code}|{row.Name}");
        foreach (var row in await db.Set<CollectionSubscriptionStatus>().AsNoTracking().OrderBy(x => x.Id)
                     .Select(x => new { Kind = "SubscriptionStatus", x.Id, x.Code, x.Name }).ToListAsync())
            Console.WriteLine($"  {row.Kind}|{row.Id}|{row.Code}|{row.Name}");
        foreach (var row in await db.Set<CollectionPaymentMethod>().AsNoTracking().OrderBy(x => x.Id)
                     .Select(x => new { Kind = "PaymentMethod", x.Id, x.Code, x.Name }).ToListAsync())
            Console.WriteLine($"  {row.Kind}|{row.Id}|{row.Code}|{row.Name}");
        foreach (var row in await db.Set<CurrencyType>().AsNoTracking().OrderBy(x => x.Id)
                     .Select(x => new { Kind = "CurrencyType", x.Id, x.Code, x.Name }).ToListAsync())
            Console.WriteLine($"  {row.Kind}|{row.Id}|{row.Code}|{row.Name}");
        foreach (var row in await db.Set<CustomerType>().AsNoTracking().OrderBy(x => x.Id)
                     .Select(x => new { Kind = "CustomerType", x.Id, x.Code, x.Name }).ToListAsync())
            Console.WriteLine($"  {row.Kind}|{row.Id}|{row.Code}|{row.Name}");
        foreach (var row in await db.Set<ServiceType>().AsNoTracking().Where(x => !x.IsDeleted).OrderBy(x => x.Id)
                     .Select(x => new { Kind = "ServiceType", x.Id, x.Name }).ToListAsync())
            Console.WriteLine($"  {row.Kind}|{row.Id}|{row.Name}");
    }
}
