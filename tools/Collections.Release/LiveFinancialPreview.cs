using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Business.Services.Crm.Collections.Calculation;

// Conservative offline review. Does not replace customer decision evidence or authorize writes.
internal static class LiveFinancialPreview
{
    private sealed record Rate(string Id, DateOnly? From, DateOnly? To, decimal? Amount,
        string Currency, int? TargetCurrencyId, short? Months, string? Behavior, string[] Issues);
    private sealed record ContractPlan(string Id, string CustomerId, string? TargetCustomerId,
        string IdentityStatus, string Status, string[] Issues, string[] Gates, Rate[] Rates);
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
    private static string Text(JsonElement r, string k) => r.TryGetProperty(k, out var v) && v.ValueKind != JsonValueKind.Null ? v.ToString().Trim() : "";
    private static string Key(JsonElement r, string k) => long.TryParse(Text(r, k), out var n) ? n.ToString(CultureInfo.InvariantCulture) : Text(r, k);
    private static string Norm(string s) => Regex.Replace(s.Trim(), @"\s+", " ").ToUpper(Tr);
    private static IEnumerable<JsonElement> Rows(string root, string file) => File.ReadLines(Path.Combine(root, file)).Select(line => { using var d = JsonDocument.Parse(line); return d.RootElement.Clone(); });
    private static JsonElement Document(string file) { using var d = JsonDocument.Parse(File.ReadAllText(file)); return d.RootElement.Clone(); }
    private static decimal? Amount(JsonElement r) => decimal.TryParse(Text(r, "Amount"), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var n)
        && Math.Abs(n) <= 9999999999999999.99m && decimal.Round(n, 2) == n ? n : null;
    private static DateOnly? Month(JsonElement r, string y = "StartingDateYear", string m = "StartingDateMonth") =>
        int.TryParse(Text(r, y), out var year) && int.TryParse(Text(r, m), out var month) && year is >= 1 and <= 9998 && month is >= 1 and <= 12 ? new DateOnly(year, month, 1) : null;
    private static DateOnly? Date(JsonElement r, string key) => DateTime.TryParse(Text(r, key), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var d) ? DateOnly.FromDateTime(d) : null;
    private static string Currency(string value) => Norm(value) switch { "TL" or "TRY" => "TRY", "EURO" or "EUR" => "EUR", "USD" => "USD", "GBP" => "GBP", _ => "" };
    private static string? Behavior(string value) => Norm(value) switch
    {
        "BAŞLANGIÇ" or "FİYAT ARTIŞI" or "FİYAT ARTISI" or "FİYAT ARTİSİ" or "FİYAT İNDİRİMİ" or "TURKCELL GPRS ZAM YANSITMASI" => "Billable",
        "ÜCRETSİZ" => "Free",
        "HİZMET DONDURMA" => "Suspended",
        _ => null
    };
    private static Task Save(string root, string file, object value) => File.WriteAllTextAsync(Path.Combine(root, file), JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));

    public static async Task RunAsync(string capturePath, string outputPath, string? decisionReplayPath = null)
    {
        var source = Path.GetFullPath(capturePath); var output = Path.GetFullPath(outputPath);
        var privateRoot = Path.GetFullPath(".tools") + Path.DirectorySeparatorChar;
        if (!source.StartsWith(privateRoot, StringComparison.OrdinalIgnoreCase) || !output.StartsWith(privateRoot, StringComparison.OrdinalIgnoreCase)
            || Directory.Exists(output) || File.Exists(output)) throw new InvalidOperationException("Yeni inceleme yalnız .tools altında oluşturulabilir.");
        foreach (var f in Document(Path.Combine(source, "package-hashes.json")).EnumerateArray())
        {
            var name = Text(f, "File");
            if (Path.GetFileName(name) != name) throw new InvalidOperationException("Kanıt dosyası yolu geçersiz.");
            if (Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(source, name)))) != Text(f, "Sha256"))
                throw new InvalidOperationException("Kaynak önizleme hash'i değişmiş.");
        }
        Directory.CreateDirectory(output);
        var approvedHistories = new HashSet<string>();
        var decisionIssues = new Dictionary<string, string[]>();
        string? replayHash = null;
        if (decisionReplayPath is not null)
        {
            var replayRoot = Path.GetFullPath(decisionReplayPath);
            if (!replayRoot.StartsWith(privateRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Karar paketi .tools altında olmalıdır.");
            CustomerDecisionReplay.VerifyPackage(replayRoot);
            var decisionSummary = Document(Path.Combine(replayRoot, "summary.json"));
            var captureHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(source, "package-hashes.json"))));
            if (Text(decisionSummary, "CapturePackageHash") != captureHash) throw new InvalidOperationException("Karar paketi başka kaynak okumaya ait.");
            foreach (var item in Document(Path.Combine(replayRoot, "billing-overrides.json")).EnumerateArray())
            {
                var history = Text(item, "HistoryId");
                if (Text(item, "Status") != "APPROVED_UNCHANGED" || !CustomerDecisionReplay.ApprovedHistoryIds.TryGetValue(history, out var expected)
                    || expected != Text(item, "ContractId")) throw new InvalidOperationException("Tanımsız müşteri onayı kullanılamaz.");
                approvedHistories.Add(history);
            }
            foreach (var item in Document(Path.Combine(replayRoot, "contract-decision-gates.json")).EnumerateArray())
                decisionIssues.Add(Text(item, "SourceContractId"), item.GetProperty("Issues").EnumerateArray().Select(x => x.GetString()!).ToArray());
            replayHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(replayRoot, "package-hashes.json"))));
        }
        var matches = Document(Path.Combine(source, "customer-matches.json")).EnumerateArray().ToDictionary(x => Text(x, "SourceId"));
        var histories = Rows(source, "histories.ndjson").ToLookup(x => Key(x, "ContractID"));
        var currencies = Rows(source, "currencies.ndjson").ToDictionary(x => Key(x, "CurrencyID"), x => Currency(Text(x, "Name")));
        var targetCurrencies = Rows(source, "live-currencies.ndjson").ToLookup(x => Currency(Text(x, "Code")));
        var services = Rows(source, "services.ndjson").ToDictionary(x => Key(x, "ServiceTypeID"), x => Norm(Text(x, "Name")));
        var targetServices = Rows(source, "live-services.ndjson").Where(x => !x.GetProperty("IsDeleted").GetBoolean()).ToLookup(x => Norm(Text(x, "Name")));
        var statuses = Rows(source, "statuses.ndjson").ToDictionary(x => Key(x, "ContractStatusID"), x => Norm(Text(x, "Name")));
        var subscriptions = Rows(source, "subscriptions.ndjson").ToDictionary(x => Key(x, "SubscriptionStatusID"), x => Norm(Text(x, "Name")));
        var plans = new List<ContractPlan>();
        foreach (var c in Rows(source, "contracts.ndjson"))
        {
            var id = Key(c, "ContractID"); var customer = Key(c, "CustomerID"); var issues = new HashSet<string>(); var gates = new HashSet<string>();
            if (decisionIssues.TryGetValue(id, out var heldDecisions)) issues.UnionWith(heldDecisions.Select(x => "CUSTOMER_DECISION_" + x));
            var match = matches.GetValueOrDefault(customer); var identity = match.ValueKind == JsonValueKind.Undefined ? "SOURCE_CUSTOMER_MISSING" : Text(match, "Status");
            if (identity == "MATCH_REQUIRES_CLASSIFICATION") gates.Add("CUSTOMER_TYPE_UPDATE_REQUIRES_APPROVAL");
            else if (identity != "IDENTITY_MATCH_CANDIDATE") issues.Add(identity);
            var statusSource = Key(c, "ContractStatusID");
            if (statuses.GetValueOrDefault(statusSource) == "YOK") issues.Add("CONTRACT_NONE_EXCLUDED");
            else if (statusSource.Length > 0 && !statuses.ContainsKey(statusSource)) issues.Add("CONTRACT_STATUS_UNKNOWN");
            var subscription = subscriptions.GetValueOrDefault(Key(c, "SubscriptionStatusID"));
            if (subscription is not ("AKTİF" or "DONUK")) issues.Add("SUBSCRIPTION_UNKNOWN");
            var start = Month(c); if (start is null) issues.Add("CONTRACT_START_INVALID");
            var end = Date(c, "EndDate"); if (Text(c, "EndDate").Length > 0 && end is null) issues.Add("CONTRACT_END_INVALID");
            if (end < start) issues.Add("CONTRACT_RANGE_REVERSED");
            var serviceName = services.GetValueOrDefault(Key(c, "ServiceTypeID"));
            if (serviceName is null) issues.Add("SERVICE_SOURCE_UNKNOWN");
            else if (!targetServices[serviceName].Any()) gates.Add("SERVICE_INSERT_REQUIRES_APPROVAL");
            else if (targetServices[serviceName].Count() > 1) issues.Add("SERVICE_TARGET_AMBIGUOUS");
            var rates = new List<Rate>();
            foreach (var h in histories[id])
            {
                var ri = new List<string>(); var from = Month(h); var inclusive = Date(h, "EndDate"); DateOnly? to = null;
                if (from is null) ri.Add("DATE_INVALID");
                if (Text(h, "EndDate").Length > 0 && (inclusive is null || inclusive == DateOnly.MaxValue)) ri.Add("END_INVALID");
                else if (inclusive is not null) to = inclusive.Value.AddDays(1);
                if (from is not null && to <= from) ri.Add("RANGE_REVERSED");
                var amount = Amount(h); if (amount is null or < 0) ri.Add("AMOUNT_INVALID");
                var currency = currencies.GetValueOrDefault(Key(h, "CurrencyID")) ?? "";
                var target = targetCurrencies[currency].ToArray(); int? currencyId = null;
                if (currency.Length == 0) ri.Add("CURRENCY_UNKNOWN");
                else if (target.Length == 0) gates.Add("CURRENCY_INSERT_REQUIRES_APPROVAL");
                else if (target.Length > 1) ri.Add("CURRENCY_TARGET_AMBIGUOUS");
                else if (int.TryParse(Key(target[0], "Id"), out var mapped) && mapped > 0) currencyId = mapped;
                var frequency = LegacyPaymentFrequencyRules.Resolve(int.TryParse(Key(h, "PaymentTypeID"), out var f) ? f : null);
                if (frequency is null) ri.Add("PAYMENT_FREQUENCY_UNKNOWN");
                var behavior = Behavior(Text(h, "ProcessType"));
                if (behavior is null && approvedHistories.Contains(Key(h, "ContractHistoryID"))
                    && Text(h, "ProcessType") == "null") behavior = "Billable";
                if (behavior is null) ri.Add("PROCESS_TYPE_DECISION_REQUIRED");
                if (Key(h, "CustomerID") != customer) ri.Add("HISTORY_CUSTOMER_MISMATCH");
                rates.Add(new(Key(h, "ContractHistoryID"), from, to, amount, currency, currencyId, frequency?.IntervalMonths, behavior, ri.ToArray()));
            }
            if (rates.Count == 0) issues.Add("HISTORY_MISSING");
            foreach (var code in rates.SelectMany(x => x.Issues)) issues.Add(code);
            var ordered = rates.Where(x => x.From.HasValue).OrderBy(x => x.From).ToArray();
            if (ordered.GroupBy(x => x.From).Any(x => x.Count() > 1)) issues.Add("PERIOD_START_DUPLICATE");
            if (ordered.Count(x => x.To is null) > 1) issues.Add("MULTIPLE_OPEN_PERIODS");
            var maximum = DateOnly.MinValue;
            foreach (var r in ordered) { if (r.From < maximum) issues.Add("PERIOD_OVERLAP"); if ((r.To ?? DateOnly.MaxValue) > maximum) maximum = r.To ?? DateOnly.MaxValue; }
            var open = rates.Where(x => x.To is null).ToArray();
            if (open.Length == 1 && (open[0].Amount != Amount(c) || open[0].Currency != (currencies.GetValueOrDefault(Key(c, "CurrencyID")) ?? "")
                || open[0].Months != LegacyPaymentFrequencyRules.Resolve(int.TryParse(Key(c, "PaymentTypeID"), out var cf) ? cf : null)?.IntervalMonths)) issues.Add("CURRENT_RATE_MISMATCH");
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "Europe/Istanbul").DateTime);
            if (subscription == "DONUK" && (end is null || end >= today) && rates.Any(x => x.Behavior == "Billable" && (x.To is null || x.To > today) && (end is null || x.From <= end)))
                issues.Add("FROZEN_EFFECTIVE_DATE_REVIEW");
            var state = issues.Count > 0 ? "HOLD_OR_EXCLUDE" : gates.Count > 0 ? "CANDIDATE_AFTER_REFERENCE_APPROVAL" : "TECHNICAL_CANDIDATE_NOT_APPROVED";
            plans.Add(new(id, customer, match.ValueKind == JsonValueKind.Undefined ? null : Text(match, "TargetId"), identity, state, issues.Order().ToArray(), gates.Order().ToArray(), rates.ToArray()));
        }
        await Save(output, "contract-review.json", plans);
        var byContract = plans.ToDictionary(x => x.Id); var paymentCounts = new Dictionary<string, long>(); var paymentTotals = new Dictionary<string, decimal>();
        long count = 0;
        await using (var file = new StreamWriter(Path.Combine(output, "payment-review.ndjson"), false, new UTF8Encoding(false)))
            foreach (var p in Rows(source, "payments.ndjson"))
            {
                var errors = new HashSet<string>(); var contractId = Key(p, "ContractID"); var contract = byContract.GetValueOrDefault(contractId);
                if (contract is null) errors.Add("SOURCE_CONTRACT_MISSING");
                else if (contract.Issues.Length > 0) errors.Add("CONTRACT_HELD_OR_EXCLUDED");
                if (contract is not null && contract.CustomerId != Key(p, "CustomerID")) errors.Add("PAYMENT_CUSTOMER_MISMATCH");
                var period = Month(p, "PeriodYear", "PeriodMonth"); if (period is null) errors.Add("PERIOD_INVALID");
                if (Date(p, "Date") is null) errors.Add("DATE_INVALID");
                var amount = Amount(p); if (amount is null or <= 0) errors.Add("AMOUNT_REVIEW");
                if (Text(p, "Free") != "Hayır") errors.Add("FREE_VALUE_REVIEW");
                if (Text(p, "Description").Length > 1000) errors.Add("DESCRIPTION_TOO_LONG");
                var currency = "";
                if (contract is not null && period is { } month)
                {
                    var due = contract.Rates.Where(r => r.Issues.Length == 0 && r.Behavior == "Billable" && r.From.HasValue && r.Months.HasValue)
                        .Where(r => CollectionPeriodRules.GetDueDates(r.From!.Value, r.To, r.Months!.Value, month, month.AddMonths(1)).Any()).ToArray();
                    var result = PaymentCurrencyCandidateRules.Evaluate(due.Select(r => new PaymentCurrencyCandidate(long.Parse(r.Id), r.TargetCurrencyId)));
                    if (result.Status != PaymentCurrencyCandidateStatus.SingleRateCurrency) errors.Add("CURRENCY_" + result.Status);
                    else currency = due.Single().Currency;
                }
                var status = errors.Count > 0 ? "HOLD_OR_EXCLUDE" : "PRELIMINARY_CANDIDATE_REQUIRES_LEGACY_EVIDENCE";
                paymentCounts[status] = paymentCounts.GetValueOrDefault(status) + 1;
                if (errors.Count == 0) paymentTotals[currency] = paymentTotals.GetValueOrDefault(currency) + amount!.Value;
                await file.WriteLineAsync(JsonSerializer.Serialize(new
                {
                    SourcePaymentId = Key(p, "PaymentID"),
                    SourceContractId = contractId,
                    SourceCustomerId = Key(p, "CustomerID"),
                    Status = status,
                    Issues = errors.Order().ToArray(),
                    Currency = currency,
                    Amount = amount,
                    ContractGates = contract?.Gates,
                    SourceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(p.GetRawText())))
                }));
                count++;
            }
        var liveCustomers = Rows(source, "live-customers.ndjson").ToDictionary(x => Key(x, "Id"));
        var liveTypes = Rows(source, "live-types.ndjson").ToArray();
        await Save(output, "classification-proposals.json", matches.Values.Where(x => Text(x, "Status") == "MATCH_REQUIRES_CLASSIFICATION")
            .GroupBy(x => Text(x, "TargetId")).Select(x =>
            {
                var live = liveCustomers[x.Key];
                var proposed = Text(x.First(), "ProposedType");
                var aliases = proposed.Split('/');
                var targetTypes = liveTypes.Where(t => aliases.Contains(Text(t, "Code"))).ToArray();
                return new
                {
                    TargetCustomerId = x.Key,
                    SourceCustomerIds = x.Select(v => Text(v, "SourceId")).ToArray(),
                    ExpectedCustomerTypeId = Key(live, "CustomerTypeId"),
                    ExpectedGroupId = Key(live, "CustomerGroupId"),
                    ExpectedTenantId = Key(live, "TenantId"),
                    ExpectedSubscriber = Text(live, "SubscriberCode"),
                    ExpectedRowHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(live.GetRawText()))),
                    CurrentType = Text(x.First(), "CurrentType"),
                    ProposedType = proposed,
                    ProposedLiveTypeId = targetTypes.Length == 1 ? Key(targetTypes[0], "Id") : null,
                    Status = targetTypes.Length == 1 ? "PREVIEW_ONLY_NO_UPDATE" : "TARGET_TYPE_AMBIGUOUS"
                };
            }));
        await Save(output, "reference-proposals.json", new
        {
            Services = Rows(source, "services.ndjson").Select(s =>
            {
                var candidates = targetServices[Norm(Text(s, "Name"))].ToArray();
                return new
                {
                    LegacyId = Key(s, "ServiceTypeID"),
                    Name = Text(s, "Name"),
                    TargetId = candidates.Length == 1 ? Key(candidates[0], "Id") : null,
                    Status = candidates.Length == 1 ? "EXISTING_MATCH" : candidates.Length == 0 ? "INSERT_PROPOSAL_REQUIRES_APPROVAL" : "AMBIGUOUS"
                };
            }),
            Currencies = Rows(source, "currencies.ndjson").Select(s =>
            {
                var code = Currency(Text(s, "Name")); var candidates = targetCurrencies[code].ToArray();
                return new
                {
                    LegacyId = Key(s, "CurrencyID"),
                    Name = Text(s, "Name"),
                    Code = code,
                    TargetId = candidates.Length == 1 ? Key(candidates[0], "Id") : null,
                    Status = code.Length == 0 ? "UNKNOWN_REVIEW" : candidates.Length == 1 ? "EXISTING_MATCH" : "MISSING_OR_AMBIGUOUS"
                };
            })
        });
        var otherSummary = new Dictionary<string, Dictionary<string, long>>();
        foreach (var (table, key, hasContract) in new[] {
            ("notes","CommentID",false),("group-history","FollowGroupStatusID",true),
            ("invoice-follow","InvoiceFollowID",false),("invoice-payments","InvoiceFollowPaymentID",false) })
        {
            var totals = new Dictionary<string, long>();
            await using var writer = new StreamWriter(Path.Combine(output, table + "-link-review.ndjson"), false, new UTF8Encoding(false));
            foreach (var row in Rows(source, table + ".ndjson"))
            {
                var sourceCustomer = Key(row, "CustomerID");
                var sourceContract = Key(row, "ContractID");
                var cp = hasContract ? byContract.GetValueOrDefault(sourceContract) : null;
                var match = matches.GetValueOrDefault(sourceCustomer);
                var status = hasContract ? (cp is null ? "SOURCE_CONTRACT_MISSING" : cp.Issues.Length > 0 ? "CONTRACT_HELD_OR_EXCLUDED" : "LINK_CANDIDATE_NEEDS_FULL_VALIDATION")
                    : match.ValueKind == JsonValueKind.Undefined ? "SOURCE_CUSTOMER_MISSING" : Text(match, "Status");
                totals[status] = totals.GetValueOrDefault(status) + 1;
                await writer.WriteLineAsync(JsonSerializer.Serialize(new
                {
                    SourceId = Key(row, key),
                    SourceCustomerId = sourceCustomer,
                    SourceContractId = sourceContract,
                    Status = status,
                    TargetCustomerId = hasContract ? cp?.TargetCustomerId :
                    match.ValueKind == JsonValueKind.Undefined ? null : Text(match, "TargetId"),
                    SourceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(row.GetRawText())))
                }));
            }
            otherSummary[table] = totals;
        }
        var bankRows = Rows(source, "bank-identities.ndjson").ToArray();
        var bankIssues = bankRows.Count(x => Text(x, "FileType").Length == 0 || Text(x, "FileRowID").Length == 0);
        var bankDuplicates = bankRows.GroupBy(x => (Text(x, "FileType"), Text(x, "FileRowID"))).Count(x => x.Count() > 1);
        await Save(output, "bank-baseline-proposal.json", new
        {
            Status = "PREVIEW_ONLY_NO_BASELINE_INSERT",
            SourceRows = bankRows.Length,
            EmptyIdentities = bankIssues,
            DuplicateLogicalIdentityGroups = bankDuplicates,
            Scope = "All legacy identities, including payments of excluded old subscriptions; identity protection does not migrate payments.",
            CaptureFile = "bank-identities.ndjson",
            CaptureSha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(source, "bank-identities.ndjson"))))
        });
        await Save(output, "summary.json", new
        {
            Status = "OFFLINE_REVIEW_NOT_APPLY_PACKAGE",
            SourceCapturePath = source,
            CustomerDecisionPackageHash = replayHash,
            ReboundApprovedHistoryCount = approvedHistories.Count,
            ConsistentCutoverSnapshot = false,
            TotalContracts = plans.Count,
            ContractResults = plans.GroupBy(x => x.Status).ToDictionary(x => x.Key, x => x.Count()),
            ContractIssues = plans.SelectMany(x => x.Issues).GroupBy(x => x).OrderByDescending(x => x.Count()).ToDictionary(x => x.Key, x => x.Count()),
            ContractGates = plans.SelectMany(x => x.Gates).GroupBy(x => x).ToDictionary(x => x.Key, x => x.Count()),
            TotalPayments = count,
            PaymentResults = paymentCounts,
            PreliminaryPaymentTotalsByCurrency = paymentTotals,
            OtherLinkResults = otherSummary,
            BankIdentityRows = bankRows.Length,
            BankEmptyIdentities = bankIssues,
            BankDuplicateLogicalIdentityGroups = bankDuplicates,
            Caveat = "Geçmiş karar kanıtları, legacy ödeme mutabakatı, tam alan/dosya/Job ve kesin kesit doğrulaması tamamlanmadan uygulanamaz."
        });
        var hashes = Directory.GetFiles(output).Order().Select(x => new { File = Path.GetFileName(x), Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(x))) });
        await Save(output, "package-hashes.json", hashes.ToArray());
        Console.WriteLine($"Çevrimdışı inceleme: {plans.Count} sözleşme, {count} ödeme. Hazırlık dosyaları üretildi; DB bağlantısı/yazması yok.");
    }
}
