using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Model.Concrete.Collections;

// Read-only source/target observation and local-file plan. No apply command exists here.
internal static class LiveTransferPreview
{
    private sealed record Capture(string File, long Rows, string Sha256, string VerificationSha256, bool Stable);
    private sealed record Match(string SourceId, string Subscriber, string? TargetId, string Status,
        string? GroupCode, string? CurrentType, string? ProposedType, string Name);
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    public static async Task RunAsync(string configurationPath, string output, bool rehearsal = false)
    {
        var outputPath = Path.GetFullPath(output);
        var privateRoot = Path.GetFullPath(".tools") + Path.DirectorySeparatorChar;
        if (!outputPath.StartsWith(privateRoot, StringComparison.OrdinalIgnoreCase)
            || Directory.Exists(outputPath) || File.Exists(outputPath))
            throw new InvalidOperationException("Kişisel veri içeren önizleme yalnız yeni .tools alt klasörüne yazılabilir.");
        using var cfg = JsonDocument.Parse(await File.ReadAllTextAsync(configurationPath), new JsonDocumentOptions
        { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var text = Environment.GetEnvironmentVariable("COLLECTION_RELEASE_CONNECTIONSTRING")
            ?? cfg.RootElement.GetProperty("AppSettings").GetProperty("MSSQLConnectionString").GetString();
        var targetBuilder = new SqlConnectionStringBuilder(text);
        if (targetBuilder.InitialCatalog != "AssistFlow") throw new InvalidOperationException("Hedef yalnız AssistFlow olabilir.");
        if (rehearsal) targetBuilder.InitialCatalog = "AssistFlow_Performance_Restore";
        targetBuilder.ApplicationIntent = ApplicationIntent.ReadOnly;
        targetBuilder.ApplicationName = "CollectionReleaseReadOnlyPreview";
        targetBuilder.ConnectTimeout = 10;
        var sourceBuilder = new SqlConnectionStringBuilder(targetBuilder.ConnectionString) { InitialCatalog = "MGS" };
        await using var target = new SqlConnection(targetBuilder.ConnectionString);
        await using var source = new SqlConnection(sourceBuilder.ConnectionString);
        await target.OpenAsync(); await source.OpenAsync();
        Directory.CreateDirectory(outputPath);
        var started = DateTimeOffset.UtcNow;
        var queries = new Dictionary<string, string>
        {
            ["customers"] = "SELECT CustomerID,SubscriberNo,Name,Type,GroupID,GroupCardNo,CreatedOn,ModifiedOn,CreatedBy,ModifiedBy FROM Core.Customer ORDER BY CustomerID",
            ["contracts"] = "SELECT ContractID,CustomerID,No,ServiceTypeID,SubscriptionStatusID,ContractStatusID,PaymentMethodID,Amount,CurrencyID,StartingDateMonth,StartingDateYear,EndDate,PaymentTypeID,GtsNo,IvrNo,InheritContractID,FileAttachmentName,FileAttachmentPath,CreatedOn,ModifiedOn,CreatedBy,ModifiedBy FROM Core.Contract ORDER BY ContractID",
            ["histories"] = "SELECT ContractHistoryID,ContractID,CustomerID,StartingDateMonth,StartingDateYear,EndDate,PaymentTypeID,Amount,ProcessType,Description,CurrencyID,CreatedOn,ModifiedOn,CreatedBy,ModifiedBy FROM Core.ContractHistory ORDER BY ContractHistoryID",
            ["payments"] = "SELECT PaymentID,CustomerID,ContractID,PeriodMonth,PeriodYear,Date,Amount,Description,Free,CreatedOn,ModifiedOn,CreatedBy,ModifiedBy FROM Core.Payment ORDER BY PaymentID",
            ["bank-identities"] = "SELECT BulkPaymentID,FileType,FileRowID FROM Core.BulkPayment ORDER BY BulkPaymentID",
            ["notes"] = "SELECT CommentID,CustomerID,Comment,CreatedOn,ModifiedOn,CreatedBy,ModifiedBy FROM Core.Comment ORDER BY CommentID",
            ["group-history"] = "SELECT FollowGroupStatusID,ContractID,PeriodMonth,PeriodYear,GroupStatusID,StatusDescription FROM Core.FollowGroupStatus ORDER BY FollowGroupStatusID",
            ["invoices"] = "SELECT InvoiceID,ContractID,No,Date,Amount,IsIntegration FROM Core.Invoice ORDER BY InvoiceID",
            ["invoice-follow"] = "SELECT InvoiceFollowID,CustomerID,No,Date,Amount,CurrencyID,Type,ProjectCode,Comment,CreatedOn,ModifiedOn,CreatedBy,ModifiedBy FROM Core.InvoiceFollow ORDER BY InvoiceFollowID",
            ["invoice-payments"] = "SELECT InvoiceFollowPaymentID,CustomerID,InvoiceFollowID,Date,Amount,Description,CreatedOn,ModifiedOn,CreatedBy,ModifiedBy FROM Core.InvoiceFollowPayment ORDER BY InvoiceFollowPaymentID",
            ["services"] = "SELECT ServiceTypeID,Name FROM Definition.ServiceType ORDER BY ServiceTypeID",
            ["currencies"] = "SELECT CurrencyID,Name FROM Definition.Currency ORDER BY CurrencyID",
            ["frequencies"] = "SELECT PaymentTypeID,Name FROM Definition.PaymentType ORDER BY PaymentTypeID",
            ["subscriptions"] = "SELECT SubscriptionStatusID,Name FROM Definition.SubscriptionStatus ORDER BY SubscriptionStatusID",
            ["statuses"] = "SELECT ContractStatusID,Name FROM Definition.ContractStatus ORDER BY ContractStatusID",
            ["payment-methods"] = "SELECT PaymentMethodID,Name FROM Definition.PaymentMethod ORDER BY PaymentMethodID",
            ["group-statuses"] = "SELECT GroupStatusID,Name FROM Definition.GroupStatus ORDER BY GroupStatusID",
            ["groups"] = "SELECT GroupID,Code,Name FROM Definition.[Group] ORDER BY GroupID"
        };
        var targetQueries = new Dictionary<string, string>
        {
            ["live-customers"] = "SELECT c.Id,c.SubscriberCode,c.SubscriberCompany,c.CustomerTypeId,c.CustomerGroupId,c.TenantId,c.IsDeleted,t.Code AS TypeCode,g.Code AS GroupCode FROM dbo.Customers c LEFT JOIN dbo.CustomerType t ON t.Id=c.CustomerTypeId LEFT JOIN dbo.CustomerGroups g ON g.Id=c.CustomerGroupId ORDER BY c.Id",
            ["live-types"] = "SELECT Id,Code,Name FROM dbo.CustomerType ORDER BY Id",
            ["live-services"] = "SELECT Id,Name,IsDeleted FROM dbo.ServiceType ORDER BY Id",
            ["live-currencies"] = "SELECT Id,Code,Name FROM dbo.CurrencyType ORDER BY Id"
        };
        var captures = new List<Capture>();
        var first = new Dictionary<string, (long Count, string Hash)>();
        foreach (var (name, sql) in queries.Concat(targetQueries))
        {
            var db = name.StartsWith("live-") ? target : source;
            var file = Path.Combine(outputPath, name + ".ndjson");
            first[name] = await CaptureAsync(db, sql, file);
            Console.WriteLine($"{name}: {first[name].Count} kayıt okundu.");
        }
        foreach (var (name, sql) in queries.Concat(targetQueries))
        {
            var second = await CaptureAsync(name.StartsWith("live-") ? target : source, sql, null);
            captures.Add(new(name + ".ndjson", first[name].Count, first[name].Hash, second.Hash,
                first[name].Count == second.Count && first[name].Hash == second.Hash));
        }
        await Save(outputPath, "capture-manifest.json", new
        {
            Status = "READ_ONLY_OBSERVATION_NOT_CUTOVER_SNAPSHOT",
            TargetDatabase = targetBuilder.InitialCatalog,
            RehearsalOnly = rehearsal,
            StartedAtUtc = started,
            CompletedAtUtc = DateTimeOffset.UtcNow,
            Isolation = "READ COMMITTED; no transaction across tables/databases",
            AllObservedHashesStable = captures.All(x => x.Stable),
            Files = captures
        });
        await BuildPlanAsync(outputPath, captures);
    }

    private static async Task<(long Count, string Hash)> CaptureAsync(SqlConnection db, string sql, string? file)
    {
        using var command = db.CreateCommand();
        command.CommandTimeout = 120;
        command.CommandText = "SET LOCK_TIMEOUT 5000; SET TRANSACTION ISOLATION LEVEL READ COMMITTED; " + sql;
        await using var reader = await command.ExecuteReaderAsync();
        await using var stream = file is null ? null : new FileStream(file, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long count = 0;
        while (await reader.ReadAsync())
        {
            var fields = new Dictionary<string, object?>();
            for (var i = 0; i < reader.FieldCount; i++) fields[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(fields) + "\n");
            hash.AppendData(bytes);
            if (stream is not null) await stream.WriteAsync(bytes);
            count++;
        }
        return (count, Convert.ToHexString(hash.GetHashAndReset()));
    }

    private static List<JsonElement> Read(string path, string name) => File.ReadLines(Path.Combine(path, name + ".ndjson"))
        .Select(line => { using var d = JsonDocument.Parse(line); return d.RootElement.Clone(); }).ToList();
    private static string Text(JsonElement row, string key) => row.TryGetProperty(key, out var x) && x.ValueKind != JsonValueKind.Null ? x.ToString().Trim() : "";
    private static string Normalize(string value) => CollectionCustomerClassification.Normalize(value);
    private static string Name(JsonElement row) => Regex.Replace(Text(row, "Name"), @"\s+", " ").ToUpper(Tr);
    private static DateTime? Created(JsonElement row) => DateTime.TryParse(Text(row, "CreatedOn"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date) ? date : null;
    private static string Id(JsonElement row, string key) => long.TryParse(Text(row, key), out var id) ? id.ToString(CultureInfo.InvariantCulture) : Text(row, key);
    private static Task Save(string root, string file, object value) => File.WriteAllTextAsync(Path.Combine(root, file), JsonSerializer.Serialize(value, Json), new UTF8Encoding(false));

    private static async Task BuildPlanAsync(string root, List<Capture> captures)
    {
        var customers = Read(root, "customers");
        var targets = Read(root, "live-customers").Where(x => !x.GetProperty("IsDeleted").GetBoolean()).ToLookup(x => Normalize(Text(x, "SubscriberCode")));
        var excluded = new Dictionary<string, string>();
        foreach (var grouping in new[] {
            customers.Where(x => Text(x,"SubscriberNo").Length > 0).GroupBy(x => Normalize(Text(x,"SubscriberNo"))),
            customers.Where(x => Name(x).Length > 0).GroupBy(Name) })
            foreach (var group in grouping.Where(x => x.Count() > 1))
            {
                var ordered = group.OrderByDescending(Created).ToList();
                var certain = ordered.All(x => Created(x).HasValue) && Created(ordered[0]) != Created(ordered[1]);
                foreach (var row in certain ? ordered.Skip(1) : ordered)
                    if (!certain || excluded.GetValueOrDefault(Id(row, "CustomerID")) != "DUPLICATE_DATE_AMBIGUOUS")
                        excluded[Id(row, "CustomerID")] = certain ? "OLDER_SUBSCRIPTION_EXCLUDED" : "DUPLICATE_DATE_AMBIGUOUS";
            }
        var matches = new List<Match>();
        foreach (var row in customers)
        {
            var id = Id(row, "CustomerID"); var subscriber = Normalize(Text(row, "SubscriberNo"));
            var type = Text(row, "Type"); var name = Text(row, "Name");
            string? reason = subscriber.Length == 0 && name.Length == 0 ? "EMPTY_IDENTITY_EXCLUDED"
                : excluded.GetValueOrDefault(id);
            var found = targets[subscriber].ToArray();
            if (reason is null && subscriber.Length == 0) reason = "SUBSCRIBER_MISSING_REVIEW";
            if (reason is null && found.Length != 1) reason = found.Length == 0 ? "LIVE_CUSTOMER_MISSING" : "LIVE_CUSTOMER_AMBIGUOUS";
            JsonElement? target = found.Length == 1 && subscriber.Length > 0 ? found[0] : null;
            var code = target is { } t ? Text(t, "GroupCode") : null;
            var current = target is { } t2 ? Text(t2, "TypeCode") : null;
            var classification = CollectionCustomerClassification.Classify(code);
            if (reason is null && classification is CollectionCustomerClass.Excluded or CollectionCustomerClass.Unknown)
                reason = classification == CollectionCustomerClass.Excluded ? "GROUP_CODE_EXCLUDED" : "GROUP_CODE_UNKNOWN";
            if (reason is null && type is "G" or "A") reason = "PARENT_OR_ACCOUNT_MAPPING_REVIEW";
            if (reason is null && CollectionCustomerClassification.Issue(code, current) is not null)
                reason = current == "BNK01" ? "BANK_TYPE_PROTECTED_REVIEW" : "MATCH_REQUIRES_CLASSIFICATION";
            var proposed = classification == CollectionCustomerClass.Individual ? "BRYSL01/N"
                : classification == CollectionCustomerClass.Group ? "GRP01/GM" : null;
            matches.Add(new(id, subscriber, target is { } t3 ? Id(t3, "Id") : null,
                reason ?? "IDENTITY_MATCH_CANDIDATE", code, current, proposed, name));
        }
        await Save(root, "customer-matches.json", matches);
        var matchById = matches.ToDictionary(x => x.SourceId);
        var contracts = Read(root, "contracts");
        var contractPlans = contracts.Select(c =>
        {
            var m = matchById.GetValueOrDefault(Id(c, "CustomerID"));
            return new
            {
                SourceContractId = Id(c, "ContractID"),
                SourceCustomerId = Id(c, "CustomerID"),
                TargetCustomerId = m?.TargetId,
                IdentityStatus = m?.Status ?? "SOURCE_CUSTOMER_MISSING",
                Status = "REQUIRES_FINANCIAL_AND_DECISION_VALIDATION",
                Payload = c
            };
        }).ToArray();
        await Save(root, "contract-candidates.json", contractPlans);
        // No payment/invoice/notefile is approved merely because its customer matches.
        var summary = new
        {
            Status = "PREPARATION_ONLY_NO_APPLY_APPROVAL",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CustomerPolicyHash = CollectionCustomerClassification.PolicyHash,
            IdentityPolicy = "item-four-latest-subscriber-and-name; missing/tied CreatedOn held; no ID fallback",
            AllObservedHashesStable = captures.All(x => x.Stable),
            ConsistentCutoverSnapshot = false,
            SourceCustomers = customers.Count,
            SourceContracts = contracts.Count,
            CustomerResults = matches.GroupBy(x => x.Status).OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Count()),
            ContractIdentityResults = contractPlans.GroupBy(x => x.IdentityStatus).OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Count()),
            FinancialApproval = "Not granted: full rate/payment/decision validation and cutover snapshot required",
            SourceCounts = captures.ToDictionary(x => x.File, x => x.Rows)
        };
        await Save(root, "summary.json", summary);
        var evidenceFiles = Directory.GetFiles(root).Order().Select(file => new
        {
            File = Path.GetFileName(file),
            Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)))
        }).ToArray();
        await Save(root, "package-hashes.json", evidenceFiles);
        Console.WriteLine($"Önizleme hazır: {customers.Count} kaynak müşteri, {contracts.Count} sözleşme. DB yazması yok; finansal uygulama onayı değildir.");
    }
}
