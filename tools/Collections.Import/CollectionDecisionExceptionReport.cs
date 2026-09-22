using System.Globalization;
using System.Text;
using System.Text.Json;
using Data.Concrete.EfCore.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Model.Concrete.Collections;

internal static class CollectionDecisionExceptionReport
{
    private static readonly HashSet<string> Codes =
    [
        "CONTRACT_NOT_INCLUDED", "PROCESS_TYPE_UNKNOWN", "PERIOD_OVERLAP",
        "CONTRACT_CURRENT_RATE_MISMATCH"
    ];

    public static async Task RunAsync(string sourceSystem, string snapshotKey, byte[] expectedManifestHash,
        string settingsPath, string snapshotDirectory)
    {
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath), new JsonDocumentOptions
        { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        var connection = new SqlConnectionStringBuilder(settings.RootElement.GetProperty("AppSettings")
            .GetProperty("MSSQLConnectionString").GetString());
        if (connection.DataSource != "192.168.1.8" || connection.InitialCatalog != "AssistFlowTest")
            throw new InvalidOperationException("Karar istisna raporu yalnız AssistFlowTest üzerinde çalışır.");
        var options = new DbContextOptionsBuilder<AppDataContext>().UseSqlServer(connection.ConnectionString, sql =>
        { sql.EnableRetryOnFailure(); sql.CommandTimeout(180); }).Options;
        await using var db = new AppDataContext(options);
        var batch = await db.Set<CollectionMigrationBatch>().AsNoTracking().SingleAsync(x =>
            x.SourceSystem == sourceSystem && x.SnapshotKey == snapshotKey);
        if (!batch.ManifestHash.SequenceEqual(expectedManifestHash))
            throw new InvalidOperationException("Staging manifest hash'i kesitle uyuşmuyor.");

        var issues = await db.Set<CollectionMigrationIssue>().AsNoTracking()
            .Where(x => x.SourceRow.BatchId == batch.Id && x.Status == CollectionMigrationIssueStatus.Open
                && Codes.Contains(x.IssueCode))
            .Select(x => new { x.SourceRowId, x.IssueCode }).ToListAsync();
        var issueByRow = issues.GroupBy(x => x.SourceRowId)
            .ToDictionary(x => x.Key, x => x.Select(i => i.IssueCode).Order().ToArray());
        var rows = await db.Set<CollectionMigrationSourceRow>().AsNoTracking()
            .Where(x => x.BatchId == batch.Id).Select(x => new { x.Id, x.EntityCode, x.SourceId, x.SourceParentId, x.Payload })
            .ToListAsync();
        var contracts = rows.Where(x => x.EntityCode == "Contract")
            .ToDictionary(x => NormalizeId(x.SourceId), x => x, StringComparer.Ordinal);
        var histories = rows.Where(x => x.EntityCode == "ContractHistory").ToList();
        var customers = rows.Where(x => x.EntityCode == "Customer")
            .ToDictionary(x => NormalizeId(x.SourceId), x => x.Payload, StringComparer.Ordinal);
        var reportDirectory = Path.Combine(snapshotDirectory, "reports");
        Directory.CreateDirectory(reportDirectory);

        var statusRows = rows.Where(x => x.EntityCode == "Contract" && issueByRow.TryGetValue(x.Id, out var c)
                && c.Contains("CONTRACT_NOT_INCLUDED"))
            .Where(x => { using var d = JsonDocument.Parse(x.Payload); var status = Text(d.RootElement, "ContractStatusID"); return string.IsNullOrWhiteSpace(status) || status == "14"; })
            .OrderBy(x => NormalizeId(x.SourceId), StringComparer.Ordinal).ToList();
        await WriteAsync(Path.Combine(reportDirectory, "question-1-contract-status.csv"),
            "LegacyContractId;LegacyCustomerId;SubscriberNo;CustomerName;ContractStatusId;StartMonth;StartYear;EndDate;CurrentAmount;CurrencyId;PaymentTypeId;IssueCodes",
            statusRows.Select(x =>
            {
                using var d = JsonDocument.Parse(x.Payload); var r = d.RootElement;
                var customerId = Text(r, "CustomerID"); var customer = Customer(customers, customerId);
                return Line(x.SourceId, customerId, customer.Subscriber, customer.Name, Text(r, "ContractStatusID"),
                    Text(r, "StartingDateMonth"), Text(r, "StartingDateYear"), Text(r, "EndDate"), Text(r, "Amount"),
                    Text(r, "CurrencyID"), Text(r, "PaymentTypeID"), string.Join(',', issueByRow[x.Id]));
            }));

        var processRows = histories.Where(x => issueByRow.TryGetValue(x.Id, out var c) && c.Contains("PROCESS_TYPE_UNKNOWN"))
            .OrderBy(x => NormalizeId(x.SourceParentId), StringComparer.Ordinal).ThenBy(x => NormalizeId(x.SourceId), StringComparer.Ordinal).ToList();
        await WriteAsync(Path.Combine(reportDirectory, "question-5-process-type.csv"),
            "LegacyHistoryId;LegacyContractId;LegacyCustomerId;SubscriberNo;CustomerName;StartMonth;StartYear;EndDate;Amount;CurrencyId;PaymentTypeId;ProcessType;Description;IssueCodes",
            processRows.Select(x =>
            {
                using var d = JsonDocument.Parse(x.Payload); var r = d.RootElement;
                contracts.TryGetValue(NormalizeId(x.SourceParentId), out var contract);
                using var cd = contract is null ? null : JsonDocument.Parse(contract.Payload);
                var customerId = Text(r, "CustomerID") ?? (cd is null ? null : Text(cd.RootElement, "CustomerID"));
                var customer = Customer(customers, customerId);
                return Line(x.SourceId, x.SourceParentId, customerId, customer.Subscriber, customer.Name,
                    Text(r, "StartingDateMonth"), Text(r, "StartingDateYear"), Text(r, "EndDate"), Text(r, "Amount"),
                    Text(r, "CurrencyID"), Text(r, "PaymentTypeID"), Text(r, "ProcessType"), Text(r, "Description"),
                    string.Join(',', issueByRow[x.Id]));
            }));

        var conflictRows = rows.Where(x => issueByRow.TryGetValue(x.Id, out var c)
                && (c.Contains("PERIOD_OVERLAP") || c.Contains("CONTRACT_CURRENT_RATE_MISMATCH")))
            .OrderBy(x => x.EntityCode).ThenBy(x => NormalizeId(x.SourceParentId ?? x.SourceId), StringComparer.Ordinal)
            .ThenBy(x => NormalizeId(x.SourceId), StringComparer.Ordinal).ToList();
        await WriteAsync(Path.Combine(reportDirectory, "question-6-rate-conflicts.csv"),
            "ProblemType;LegacyContractId;LegacyHistoryId;LegacyCustomerId;SubscriberNo;CustomerName;StartMonth;StartYear;EndDate;Amount;CurrencyId;PaymentTypeId;ProcessType;Description;IssueCodes",
            conflictRows.Select(x =>
            {
                using var d = JsonDocument.Parse(x.Payload); var r = d.RootElement;
                var contractId = x.EntityCode == "Contract" ? x.SourceId : x.SourceParentId;
                contracts.TryGetValue(NormalizeId(contractId), out var contract);
                using var cd = contract is null ? null : JsonDocument.Parse(contract.Payload);
                var customerId = Text(r, "CustomerID") ?? (cd is null ? null : Text(cd.RootElement, "CustomerID"));
                var customer = Customer(customers, customerId);
                var codes = issueByRow[x.Id];
                return Line(codes.Contains("PERIOD_OVERLAP") ? "Tarife tarihleri çakışıyor" : "Güncel tutar ile son tarihçe farklı",
                    contractId, x.EntityCode == "ContractHistory" ? x.SourceId : null, customerId, customer.Subscriber,
                    customer.Name, Text(r, "StartingDateMonth"), Text(r, "StartingDateYear"), Text(r, "EndDate"),
                    Text(r, "Amount"), Text(r, "CurrencyID"), Text(r, "PaymentTypeID"), Text(r, "ProcessType"),
                    Text(r, "Description"), string.Join(',', codes));
            }));

        Console.WriteLine($"Karar raporları oluşturuldu: madde 1={statusRows.Count}, madde 5={processRows.Count}, madde 6={conflictRows.Count}.");
    }

    private static (string? Subscriber, string? Name) Customer(Dictionary<string, string> customers, string? id)
    {
        if (!customers.TryGetValue(NormalizeId(id), out var payload)) return (null, null);
        using var d = JsonDocument.Parse(payload);
        return (Text(d.RootElement, "SubscriberNo"), Text(d.RootElement, "Name"));
    }
    private static async Task WriteAsync(string path, string header, IEnumerable<string> lines)
    { await using var writer = new StreamWriter(path, false, new UTF8Encoding(true)); await writer.WriteLineAsync(header); foreach (var line in lines) await writer.WriteLineAsync(line); }
    private static string Line(params string?[] values) => string.Join(';', values.Select(Csv));
    private static string? Text(JsonElement root, string property)
    { if (!root.TryGetProperty(property, out var v) || v.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null; var s = v.ValueKind == JsonValueKind.String ? v.GetString() : v.GetRawText(); return string.IsNullOrWhiteSpace(s) ? null : s.Trim(); }
    private static string NormalizeId(string? value) => long.TryParse(value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id.ToString(CultureInfo.InvariantCulture) : value?.Trim() ?? string.Empty;
    private static string Csv(string? value) { if (string.IsNullOrEmpty(value)) return string.Empty; var safe = value[0] is '=' or '+' or '-' or '@' ? $"'{value}" : value; return $"\"{safe.Replace("\"", "\"\"")}\""; }
}
