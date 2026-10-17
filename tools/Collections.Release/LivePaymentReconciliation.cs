using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Business.Services.Crm.Collections.Calculation;
using Microsoft.Data.SqlClient;

// Preparation only. Fixed SELECTs for definition evidence; all comparisons/output are local.
internal static class LivePaymentReconciliation
{
    private static string Text(JsonElement r, string k) => r.TryGetProperty(k, out var v) && v.ValueKind != JsonValueKind.Null ? v.ToString().Trim() : "";
    private static long Id(JsonElement r, string k) => long.TryParse(Text(r, k), out var id) ? id : 0;
    private static JsonElement Document(string path) { using var d = JsonDocument.Parse(File.ReadAllText(path)); return d.RootElement.Clone(); }
    private static IEnumerable<JsonElement> Rows(string root, string file) => File.ReadLines(Path.Combine(root, file)).Select(line => { using var d = JsonDocument.Parse(line); return d.RootElement.Clone(); });
    private static string Hash(string path) { using var file = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(file)); }
    private static Task Save(string root, string file, object value) => File.WriteAllTextAsync(Path.Combine(root, file), JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
    private static void Require(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
    private static void Increment(Dictionary<string, long> counts, string key) => counts[key] = counts.GetValueOrDefault(key) + 1;

    public static async Task RunAsync(string capturePath, string reviewPath, string configPath, string outputPath)
    {
        var capture = Path.GetFullPath(capturePath); var review = Path.GetFullPath(reviewPath); var output = Path.GetFullPath(outputPath);
        var privateRoot = Path.GetFullPath(".tools") + Path.DirectorySeparatorChar;
        Require(new[] { capture, review, output }.All(x => x.StartsWith(privateRoot, StringComparison.OrdinalIgnoreCase))
            && !Directory.Exists(output) && !File.Exists(output), "Yeni mutabakat paketi yalnız .tools altında oluşturulabilir.");
        CustomerDecisionReplay.VerifyPackage(capture); CustomerDecisionReplay.VerifyPackage(review);
        var reviewSummary = Document(Path.Combine(review, "summary.json"));
        Require(Path.GetFullPath(Text(reviewSummary, "SourceCapturePath")) == capture && Text(reviewSummary, "CustomerDecisionPackageHash").Length == 64,
            "Güncel kesite bağlı karar incelemesi gereklidir.");
        using var cfg = JsonDocument.Parse(await File.ReadAllTextAsync(configPath), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var connectionText = Environment.GetEnvironmentVariable("COLLECTION_RELEASE_CONNECTIONSTRING") ?? cfg.RootElement.GetProperty("AppSettings").GetProperty("MSSQLConnectionString").GetString();
        var builder = new SqlConnectionStringBuilder(connectionText) { ApplicationIntent = ApplicationIntent.ReadOnly, ConnectTimeout = 10, ApplicationName = "CollectionPaymentEvidenceReadOnly" };
        Require(builder.InitialCatalog == "AssistFlow", "Bağlantı hedefi AssistFlow olmalıdır; kanıt MGS üzerinden yalnız okunur.");
        builder.InitialCatalog = "MGS";
        await using var connection = new SqlConnection(builder.ConnectionString); await connection.OpenAsync();
        var queries = new[] {
            "SELECT CurrencyID,Name FROM Definition.Currency ORDER BY CurrencyID",
            "SELECT PaymentTypeID,Name FROM Definition.PaymentType ORDER BY PaymentTypeID",
            "SELECT CONVERT(nvarchar(128),DATABASEPROPERTYEX(DB_NAME(),'Collation')) AS DatabaseCollation,YEAR(GETDATE()) AS SourceYear",
            "SELECT s.name AS SchemaName,o.name AS ObjectName,m.definition AS Definition FROM sys.objects o JOIN sys.schemas s ON s.schema_id=o.schema_id JOIN sys.sql_modules m ON m.object_id=o.object_id WHERE (s.name='Core' AND o.name IN ('vPayment','vCustomerContractHistory','vContractHistory','vContract','vCustomer')) OR (s.name='dbo' AND o.name IN ('vPaymentCurrency','TarihTablosu','fnTahsilatTakibi','fnTahsilatTakibiGrup')) ORDER BY s.name,o.name"
        };
        async Task<JsonElement[]> ReadEvidence()
        {
            var results = new List<JsonElement>();
            foreach (var sql in queries)
            {
                using var command = connection.CreateCommand(); command.CommandTimeout = 60;
                command.CommandText = "SET LOCK_TIMEOUT 5000; " + sql;
                await using var reader = await command.ExecuteReaderAsync();
                var rows = new List<Dictionary<string, object?>>();
                while (await reader.ReadAsync())
                {
                    var row = new Dictionary<string, object?>();
                    for (var i = 0; i < reader.FieldCount; i++) row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    rows.Add(row);
                }
                results.Add(JsonSerializer.SerializeToElement(rows));
            }
            return results.ToArray();
        }
        var evidence = await ReadEvidence(); var verification = await ReadEvidence();
        Require(JsonSerializer.Serialize(evidence) == JsonSerializer.Serialize(verification), "İki kanıt okuması farklı; paket oluşturulmadı.");
        Require(evidence[0].GetRawText() == JsonSerializer.Serialize(Rows(capture, "currencies.ndjson"))
            && evidence[1].GetRawText() == JsonSerializer.Serialize(Rows(capture, "frequencies.ndjson")), "Kaynak tanımları kesitten sonra değişmiş; yeni kesit gerekir.");
        Require(evidence[2].GetArrayLength() == 1 && Text(evidence[2][0], "DatabaseCollation") == "Turkish_CI_AS", "Legacy collation desteklenmiyor.");
        var sourceYear = checked((int)Id(evidence[2][0], "SourceYear"));
        Require(sourceYear is >= 1753 and < 9998, "Kaynak yılı geçersiz.");
        var definitions = evidence[3].EnumerateArray().ToDictionary(x => Text(x, "SchemaName") + "." + Text(x, "ObjectName"));
        foreach (var (name, expected) in CollectionPaymentCurrencyComparison.DefinitionHashes)
        {
            Require(definitions.TryGetValue(name, out var definition), "Legacy SQL kanıtı eksik: " + name);
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(definition.GetProperty("Definition").GetString()!)));
            Require(hash == expected, "Legacy SQL tanımı değişmiş; tekrar inceleme gerekir: " + name);
        }
        Directory.CreateDirectory(output);
        await Save(output, "legacy-definition-evidence.json", evidence);
        var refs = Document(Path.Combine(review, "reference-proposals.json")).GetProperty("Currencies").EnumerateArray().Where(x => Text(x, "Status") == "EXISTING_MATCH").ToArray();
        var maps = refs.ToDictionary(x => Id(x, "LegacyId"), x => checked((int)Id(x, "TargetId")));
        var codes = refs.ToDictionary(x => Text(x, "Code"), x => checked((int)Id(x, "TargetId")));
        var customers = Rows(capture, "customers.ndjson").ToDictionary(x => Id(x, "CustomerID"));
        // Date representation only: the previous snapshot reader wrote O; SQL capture omits trailing zero fractions.
        var histories = Rows(capture, "histories.ndjson").Select(row =>
        {
            if (Text(row, "EndDate").Length == 0) return row;
            Require(DateTime.TryParse(Text(row, "EndDate"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date), "Tarihçe bitişi okunamadı.");
            var node = JsonNode.Parse(row.GetRawText())!; node["EndDate"] = date.ToString("O", CultureInfo.InvariantCulture);
            return JsonSerializer.SerializeToElement(node);
        }).ToLookup(x => Id(x, "ContractID"));
        var currencies = evidence[0].EnumerateArray().ToDictionary(x => Id(x, "CurrencyID"), x => (string?)Text(x, "Name"));
        var frequencies = evidence[1].EnumerateArray().ToDictionary(x => Id(x, "PaymentTypeID"), x => (string?)Text(x, "Name"));
        var resolve = CollectionPaymentCurrencyComparison.CreateResolver(customers, histories, currencies, frequencies, maps, new DateOnly(sourceYear + 1, 12, 31));
        var retained = Document(Path.Combine(review, "contract-review.json")).EnumerateArray().Where(x => Text(x, "Status") is "CANDIDATE_AFTER_REFERENCE_APPROVAL" or "TECHNICAL_CANDIDATE_NOT_APPROVED").Select(x => Id(x, "Id")).ToHashSet();
        var rawPayments = Rows(capture, "payments.ndjson").ToDictionary(x => Id(x, "PaymentID"));
        var counts = new Dictionary<string, long>(); var issueCounts = new Dictionary<string, long>();
        var totals = new Dictionary<string, decimal>(); var currencyCounts = new Dictionary<string, long>();
        var sourceIds = new HashSet<long>(); var requestIds = new HashSet<Guid>();
        long read = 0, candidates = 0, removedPrior = 0, added = 0, free = 0, zero = 0, negative = 0;
        await using (var report = new StreamWriter(Path.Combine(output, "payment-reconciliation.ndjson"), false, new UTF8Encoding(false)))
        await using (var plan = new StreamWriter(Path.Combine(output, "payment-import-plan.ndjson"), false, new UTF8Encoding(false)))
            foreach (var row in Rows(review, "payment-review.ndjson"))
            {
                read++; var id = Id(row, "SourcePaymentId"); var raw = rawPayments[id];
                Require(sourceIds.Add(id) && Text(row, "SourceHash") == Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw.GetRawText()))), "Ödeme kimliği/hash'i uyuşmuyor.");
                var issues = row.GetProperty("Issues").EnumerateArray().Select(x => x.GetString()!).ToHashSet();
                var validAmount = decimal.TryParse(Text(raw, "Amount"), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount)
                    && Math.Abs(amount) <= 9999999999999999.99m && decimal.Round(amount, 2) == amount;
                if (validAmount) issues.Remove("AMOUNT_REVIEW");
                var freeText = Text(raw, "Free");
                if (freeText is "Evet" or "Hayır") issues.Remove("FREE_VALUE_REVIEW");
                var comparison = "CONTRACT_HELD_OR_EXCLUDED";
                if (retained.Contains(Id(raw, "ContractID")) && int.TryParse(Text(raw, "PeriodYear"), out var year) && year is >= 1 and <= 9998
                    && int.TryParse(Text(raw, "PeriodMonth"), out var month) && month is >= 1 and <= 12)
                {
                    var legacy = resolve(Id(raw, "ContractID"), new DateOnly(year, month, 1));
                    comparison = legacy.InvalidHistory ? "LEGACY_INVALID_HISTORY"
                        : legacy.Result.Status != PaymentCurrencyCandidateStatus.SingleRateCurrency ? "LEGACY_" + legacy.Result.Status
                        : !codes.TryGetValue(Text(row, "Currency"), out var targetCurrency) ? "TARGET_CURRENCY_UNRESOLVED"
                        : legacy.Result.ResolvedCurrencyTypeId != targetCurrency ? "CURRENCY_MISMATCH" : "SINGLE_CURRENCY_MATCH";
                }
                if (comparison != "SINGLE_CURRENCY_MATCH") issues.Add(comparison);
                var candidate = issues.Count == 0;
                var prior = Text(row, "Status") == "PRELIMINARY_CANDIDATE_REQUIRES_LEGACY_EVIDENCE";
                Increment(counts, comparison); foreach (var issue in issues) Increment(issueCounts, issue);
                if (prior && !candidate) removedPrior++;
                if (candidate)
                {
                    candidates++; if (!prior) added++; if (freeText == "Evet") free++; if (amount == 0) zero++; if (amount < 0) negative++;
                    var currency = Text(row, "Currency"); totals[currency] = totals.GetValueOrDefault(currency) + amount; Increment(currencyCounts, currency);
                    var requestId = CollectionPaymentImportIdentity.For(id); Require(requestIds.Add(requestId), "Mükerrer ödeme işlem kimliği.");
                    await plan.WriteLineAsync(JsonSerializer.Serialize(new
                    {
                        SourcePaymentId = id,
                        RequestId = requestId,
                        SourceHash = Text(row, "SourceHash"),
                        SourceContractId = Id(raw, "ContractID"),
                        Currency = currency,
                        CurrencyTypeId = codes[currency],
                        Amount = amount,
                        IsFree = freeText == "Evet",
                        Source = raw,
                        ReadyToApply = false
                    }));
                }
                await report.WriteLineAsync(JsonSerializer.Serialize(new
                {
                    SourcePaymentId = id,
                    PriorCandidate = prior,
                    Comparison = comparison,
                    Candidate = candidate,
                    Issues = issues.Order(),
                    Currency = Text(row, "Currency"),
                    Amount = validAmount ? (decimal?)amount : null,
                    IsFree = freeText == "Evet",
                    ReadyToApply = false
                }));
            }
        Require(read == rawPayments.Count && read == Id(reviewSummary, "TotalPayments"), "Ödeme kapsamı/sayımı uyuşmuyor.");
        var banks = Rows(capture, "bank-identities.ndjson").ToArray(); var identities = new HashSet<(string, string)>();
        var bankIds = new HashSet<long>(); var bankCounts = new Dictionary<string, long>(); long bankIssues = 0;
        await using (var baseline = new StreamWriter(Path.Combine(output, "bank-baseline-plan.ndjson"), false, new UTF8Encoding(false)))
            foreach (var row in banks)
            {
                var type = Text(row, "FileType"); var number = Text(row, "FileRowID").ToUpperInvariant();
                var valid = type is "GTS" or "IVR" && number.Length > 0 && bankIds.Add(Id(row, "BulkPaymentID")) && identities.Add((type, number));
                if (!valid) bankIssues++;
                Increment(bankCounts, type);
                await baseline.WriteLineAsync(JsonSerializer.Serialize(new
                {
                    SourceId = Id(row, "BulkPaymentID"),
                    Type = type,
                    TransactionNumber = number,
                    Valid = valid,
                    Source = row,
                    ReadyToApply = false
                }));
            }
        await Save(output, "summary.json", new
        {
            Status = "PREPARATION_NOT_APPROVED_FOR_EXECUTION",
            CapturePackageHash = Hash(Path.Combine(capture, "package-hashes.json")),
            ReviewPackageHash = Hash(Path.Combine(review, "package-hashes.json")),
            CustomerDecisionPackageHash = Text(reviewSummary, "CustomerDecisionPackageHash"),
            EvidenceReadAtUtc = DateTimeOffset.UtcNow,
            SourceYear = sourceYear,
            DefinitionCount = definitions.Count,
            TotalPayments = read,
            Candidates = candidates,
            HeldOrExcluded = read - candidates,
            RemovedPriorCandidates = removedPrior,
            AddedHistoricalCandidates = added,
            HistoricalObservations = new { Free = free, Zero = zero, Negative = negative },
            ComparisonCounts = counts,
            IssueCounts = issueCounts,
            CandidateTotalsByCurrency = totals,
            CandidateCountsByCurrency = currencyCounts,
            BankRows = banks.Length,
            BankCounts = bankCounts,
            BankIssues = bankIssues,
            BankScope = "All source identities; excluded subscriptions stay protected without migrating their payments. Apply must use existing CollectionBankLoadService.Key; no payment-row linkage is inferred.",
            RemainingGates = new[] { "Final maintenance snapshot and live references", "Customer/contract/reference approvals", "Actor mapping and audit receipts", "Isolated restore/idempotency/rollback rehearsal", "Bank baseline apply with existing key algorithm", "Group/cari/invoice/file reconciliation" },
            ReadyToApply = false
        });
        await Save(output, "package-hashes.json", Directory.GetFiles(output).Order().Select(x => new { File = Path.GetFileName(x), Sha256 = Hash(x) }).ToArray());
        Console.WriteLine($"Ödeme mutabakatı: {read} satır, {candidates} aday, {added} tarihsel ek aday, {removedPrior} önceki aday incelemeye alındı. Banka kimliği {banks.Length}, sorun {bankIssues}. DB'ye yazılmadı.");
    }
}
