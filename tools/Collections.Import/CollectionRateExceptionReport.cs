using System.Globalization;
using System.Text;
using System.Text.Json;
using Data.Concrete.EfCore.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Model.Concrete.Collections;

internal static class CollectionRateExceptionReport
{
    private static readonly HashSet<string> RateIssueCodes = ["CURRENCY_MAP_MISSING", "AMOUNT_INVALID"];

    public static async Task RunAsync(string sourceSystem, string snapshotKey, byte[] expectedManifestHash,
        string settingsPath, string snapshotDirectory)
    {
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath), new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        });
        var connection = new SqlConnectionStringBuilder(settings.RootElement.GetProperty("AppSettings")
            .GetProperty("MSSQLConnectionString").GetString());
        if (connection.DataSource != "192.168.1.8" || connection.InitialCatalog != "AssistFlowTest")
            throw new InvalidOperationException("Tarife istisna raporu yalnız AssistFlowTest üzerinde çalışır.");
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

        var issues = await db.Set<CollectionMigrationIssue>().AsNoTracking()
            .Where(x => x.SourceRow.BatchId == batch.Id && x.Status == CollectionMigrationIssueStatus.Open
                && RateIssueCodes.Contains(x.IssueCode))
            .Select(x => new { x.SourceRowId, x.IssueCode }).ToListAsync();
        var issueByRow = issues.GroupBy(x => x.SourceRowId)
            .ToDictionary(x => x.Key, x => x.Select(i => i.IssueCode).Order().ToArray());
        var rowIds = issueByRow.Keys.ToHashSet();
        var rateRows = await db.Set<CollectionMigrationRatePeriodStage>().AsNoTracking()
            .Include(x => x.SourceRow)
            .Where(x => x.SourceRow.BatchId == batch.Id && rowIds.Contains(x.SourceRowId)
                && x.Status == CollectionMigrationRowStatus.Blocked)
            .ToListAsync();

        var sourceRows = await db.Set<CollectionMigrationSourceRow>().AsNoTracking()
            .Where(x => x.BatchId == batch.Id && (x.EntityCode == "Contract" || x.EntityCode == "Customer"))
            .Select(x => new { x.EntityCode, x.SourceId, x.Payload }).ToListAsync();
        var contracts = sourceRows.Where(x => x.EntityCode == "Contract")
            .ToDictionary(x => NormalizeId(x.SourceId), x => ParseContract(x.Payload), StringComparer.Ordinal);
        var customers = sourceRows.Where(x => x.EntityCode == "Customer")
            .ToDictionary(x => NormalizeId(x.SourceId), x => ParseCustomer(x.Payload), StringComparer.Ordinal);

        var reportDirectory = Path.Combine(snapshotDirectory, "reports");
        Directory.CreateDirectory(reportDirectory);
        var reportPath = Path.Combine(reportDirectory, "rate-currency-amount-exceptions.csv");
        await using var writer = new StreamWriter(reportPath, false, new UTF8Encoding(true));
        await writer.WriteLineAsync("LegacyContractHistoryId;LegacyContractId;LegacyCustomerId;SubscriberNo;CustomerName;StartMonth;StartYear;EndDate;PaymentTypeId;RawCurrencyId;RawAmount;ProcessType;Description;IssueCodes");
        foreach (var stage in rateRows.OrderBy(x => NormalizeId(x.SourceContractId), StringComparer.Ordinal)
                     .ThenBy(x => x.EffectiveFrom).ThenBy(x => NormalizeId(x.SourceRow.SourceId), StringComparer.Ordinal))
        {
            using var document = JsonDocument.Parse(stage.SourceRow.Payload);
            var root = document.RootElement;
            contracts.TryGetValue(NormalizeId(stage.SourceContractId), out var contract);
            var customerId = Text(root, "CustomerID") ?? contract?.CustomerId;
            customers.TryGetValue(NormalizeId(customerId), out var customer);
            await writer.WriteLineAsync(string.Join(';', new[]
            {
                Csv(stage.SourceRow.SourceId), Csv(stage.SourceContractId), Csv(customerId),
                Csv(customer?.SubscriberNo), Csv(customer?.Name), Csv(Text(root, "StartingDateMonth")),
                Csv(Text(root, "StartingDateYear")), Csv(Text(root, "EndDate")),
                Csv(Text(root, "PaymentTypeID")), Csv(Text(root, "CurrencyID")), Csv(Text(root, "Amount")),
                Csv(Text(root, "ProcessType")), Csv(Text(root, "Description")),
                Csv(string.Join(',', issueByRow[stage.SourceRowId]))
            }));
        }
        Console.WriteLine($"Tarife para birimi/tutar mutabakat raporu: {rateRows.Count} tekil tarihçe kaydı.");
        foreach (var group in issues.GroupBy(x => x.IssueCode).OrderByDescending(x => x.Count()))
            Console.WriteLine($"  {group.Key}|{group.Count()}");
        Console.WriteLine($"Rapor Git dışındaki kesit alanına yazıldı: {reportPath}");
    }

    private static ContractSource ParseContract(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        return new ContractSource(Text(document.RootElement, "CustomerID"));
    }

    private static CustomerSource ParseCustomer(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        return new CustomerSource(Text(document.RootElement, "SubscriberNo"), Text(document.RootElement, "Name"));
    }

    private static string? Text(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var value)
            || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;
        var text = value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    private static string NormalizeId(string? value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        return long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
            ? id.ToString(CultureInfo.InvariantCulture) : trimmed;
    }

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var safe = value[0] is '=' or '+' or '-' or '@' ? $"'{value}" : value;
        return $"\"{safe.Replace("\"", "\"\"")}\"";
    }

    private sealed record ContractSource(string? CustomerId);
    private sealed record CustomerSource(string? SubscriberNo, string? Name);
}
