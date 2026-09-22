using System.Globalization;
using System.Text;
using System.Text.Json;
using Data.Concrete.EfCore.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Model.Concrete;
using Model.Concrete.Collections;

internal static class CollectionCustomerExceptionReport
{
    private static readonly HashSet<string> CustomerIssueCodes =
    [
        "CUSTOMER_ORPHAN", "SUBSCRIBER_BLANK", "SUBSCRIBER_TOO_LONG",
        "SUBSCRIBER_SOURCE_DUPLICATE", "CUSTOMER_TARGET_MISSING",
        "CUSTOMER_TARGET_AMBIGUOUS", "CUSTOMER_TARGET_DELETED",
        "CUSTOMER_TYPE_MAP_MISSING", "CUSTOMER_TYPE_MISMATCH"
    ];

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
            throw new InvalidOperationException("Müşteri istisna raporu yalnız AssistFlowTest üzerinde çalışır.");
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
            throw new InvalidOperationException("Müşteri istisna raporu yalnız inceleme bekleyen kesitte alınabilir.");

        var issues = await db.Set<CollectionMigrationIssue>().AsNoTracking()
            .Where(x => x.SourceRow.BatchId == batch.Id && x.Status == CollectionMigrationIssueStatus.Open
                && CustomerIssueCodes.Contains(x.IssueCode))
            .Select(x => new { x.SourceRowId, x.IssueCode }).ToListAsync();
        var issueByRow = issues.GroupBy(x => x.SourceRowId)
            .ToDictionary(x => x.Key, x => x.Select(i => i.IssueCode).Order().ToArray());
        var rowIds = issueByRow.Keys.ToHashSet();
        var contracts = await db.Set<CollectionMigrationContractStage>().AsNoTracking()
            .Include(x => x.SourceRow)
            .Where(x => x.SourceRow.BatchId == batch.Id && rowIds.Contains(x.SourceRowId)
                && x.Status == CollectionMigrationRowStatus.Blocked)
            .ToListAsync();

        var customerRows = await db.Set<CollectionMigrationSourceRow>().AsNoTracking()
            .Where(x => x.BatchId == batch.Id && x.EntityCode == "Customer")
            .Select(x => new { x.SourceId, x.Payload }).ToListAsync();
        var sourceCustomers = customerRows.ToDictionary(x => NormalizeId(x.SourceId), x => ParseCustomer(x.Payload),
            StringComparer.Ordinal);
        var acceptedTypeMaps = await db.Set<CollectionMigrationReferenceMap>().AsNoTracking()
            .Where(x => x.BatchId == batch.Id && x.ReferenceKind == "CustomerType"
                && x.Status == CollectionMigrationDecisionStatus.Accepted)
            .Select(x => new { x.SourceId, x.TargetCustomerTypeId }).ToListAsync();
        var expectedTypes = acceptedTypeMaps.ToDictionary(x => NormalizeId(x.SourceId),
            x => x.TargetCustomerTypeId, StringComparer.Ordinal);
        var targetIds = contracts.Where(x => x.TargetCustomerId.HasValue)
            .Select(x => x.TargetCustomerId!.Value).Distinct().ToList();
        var targetCustomers = await db.Set<Customer>().AsNoTracking().Where(x => targetIds.Contains(x.Id))
            .Select(x => new { x.Id, x.CustomerTypeId }).ToDictionaryAsync(x => x.Id);
        var typeIds = expectedTypes.Values.Where(x => x.HasValue).Select(x => x!.Value)
            .Concat(targetCustomers.Values.Where(x => x.CustomerTypeId.HasValue)
                .Select(x => x.CustomerTypeId!.Value)).Distinct().ToList();
        var customerTypes = await db.Set<CustomerType>().AsNoTracking().Where(x => typeIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Code, x.Name }).ToDictionaryAsync(x => x.Id);

        var reportRows = contracts.Select(contract =>
        {
            var customerKey = NormalizeId(contract.SourceCustomerId);
            sourceCustomers.TryGetValue(customerKey, out var sourceCustomer);
            var sourceType = sourceCustomer?.Type;
            var expectedTypeId = sourceType is not null
                && expectedTypes.TryGetValue(NormalizeId(sourceType), out var expected) ? expected : null;
            var actualTypeId = contract.TargetCustomerId.HasValue
                && targetCustomers.TryGetValue(contract.TargetCustomerId.Value, out var target)
                ? target.CustomerTypeId : null;
            return new ReportRow(contract.SourceRow.SourceId, contract.SourceCustomerId,
                sourceCustomer?.SubscriberNo, contract.TargetCustomerId, sourceType,
                expectedTypeId, TypeLabel(expectedTypeId), actualTypeId, TypeLabel(actualTypeId),
                string.Join(',', issueByRow[contract.SourceRowId]));
        }).OrderBy(x => x.IssueCodes, StringComparer.Ordinal)
          .ThenBy(x => NormalizeId(x.ContractSourceId), StringComparer.Ordinal).ToList();

        var reportDirectory = Path.Combine(snapshotDirectory, "reports");
        Directory.CreateDirectory(reportDirectory);
        var reportPath = Path.Combine(reportDirectory, "customer-exceptions.csv");
        await using (var writer = new StreamWriter(reportPath, false, new UTF8Encoding(true)))
        {
            await writer.WriteLineAsync("LegacyContractId;LegacyCustomerId;SubscriberNo;TargetCustomerId;SourceCustomerType;ExpectedCustomerTypeId;ExpectedCustomerType;ActualCustomerTypeId;ActualCustomerType;IssueCodes");
            foreach (var row in reportRows)
                await writer.WriteLineAsync(string.Join(';', new[]
                {
                    Csv(row.ContractSourceId), Csv(row.SourceCustomerId), Csv(row.SubscriberNo),
                    Csv(row.TargetCustomerId?.ToString(CultureInfo.InvariantCulture)), Csv(row.SourceCustomerType),
                    Csv(row.ExpectedCustomerTypeId?.ToString(CultureInfo.InvariantCulture)), Csv(row.ExpectedCustomerType),
                    Csv(row.ActualCustomerTypeId?.ToString(CultureInfo.InvariantCulture)), Csv(row.ActualCustomerType),
                    Csv(row.IssueCodes)
                }));
        }

        Console.WriteLine($"Müşteri mutabakat raporu: {reportRows.Count} tekil sözleşme.");
        foreach (var group in reportRows.SelectMany(x => x.IssueCodes.Split(','))
                     .GroupBy(x => x).OrderByDescending(x => x.Count()).ThenBy(x => x.Key))
            Console.WriteLine($"  {group.Key}|{group.Count()}");
        Console.WriteLine("Müşteri türü uyuşmazlık matrisi (beklenen -> mevcut):");
        foreach (var group in reportRows.Where(x => x.IssueCodes.Contains("CUSTOMER_TYPE_MISMATCH", StringComparison.Ordinal))
                     .GroupBy(x => new { x.ExpectedCustomerType, x.ActualCustomerType })
                     .OrderByDescending(x => x.Count()).ThenBy(x => x.Key.ExpectedCustomerType)
                     .ThenBy(x => x.Key.ActualCustomerType))
            Console.WriteLine($"  {group.Key.ExpectedCustomerType} -> {group.Key.ActualCustomerType}|{group.Count()}");
        Console.WriteLine($"Rapor Git dışındaki kesit alanına yazıldı: {reportPath}");

        string? TypeLabel(long? id) => id.HasValue && customerTypes.TryGetValue(id.Value, out var type)
            ? $"{type.Code} - {type.Name}" : null;
    }

    private static SourceCustomer ParseCustomer(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        return new SourceCustomer(Text(document.RootElement, "SubscriberNo"), Text(document.RootElement, "Type"));
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

    private sealed record SourceCustomer(string? SubscriberNo, string? Type);
    private sealed record ReportRow(string ContractSourceId, string? SourceCustomerId, string? SubscriberNo,
        long? TargetCustomerId, string? SourceCustomerType, long? ExpectedCustomerTypeId,
        string? ExpectedCustomerType, long? ActualCustomerTypeId, string? ActualCustomerType, string IssueCodes);
}
