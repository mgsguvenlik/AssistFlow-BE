using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Business.Services.Crm.Collections.Calculation;
using Data.Concrete.EfCore.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Model.Concrete.Collections;

/// <summary>Salt-okunur ön inceleme. Çıktısı bir ödeme uygulama planı değildir.</summary>
internal static class CollectionPaymentReviewer
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly (string File, string Entity, string Key)[] Files =
    [
        ("customers.ndjson", "Customer", "CustomerID"),
        ("contracts.ndjson", "Contract", "ContractID"),
        ("contract-history.ndjson", "ContractHistory", "ContractHistoryID"),
        ("payments.ndjson", "Payment", "PaymentID")
    ];

    public static async Task<string> RunAsync(string directory, string settingsPath, long batchId, string expectedHash,
        AppDataContext? transactionContext = null)
    {
        if (batchId <= 0) throw new InvalidOperationException("Geçerli sözleşme aktarım batch kimliği gereklidir.");
        directory = Path.GetFullPath(directory);
        var manifestBytes = await File.ReadAllBytesAsync(Path.Combine(directory, "manifest.json"));
        var manifestHash = Convert.ToHexString(SHA256.HashData(manifestBytes));
        if (!string.Equals(manifestHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Ödeme manifest hash'i beklenen kesitle uyuşmuyor.");
        using var manifest = JsonDocument.Parse(manifestBytes);
        var metadata = manifest.RootElement;
        if (Text(metadata, "sourceSystem") != "MGS" || Text(metadata, "exportKind") != "payment-review"
            || Text(metadata, "ruleVersion") != "payment-review-v1" || Text(metadata, "normalizationVersion") != "subscriber-v1")
            throw new InvalidOperationException("Desteklenen MGS ödeme inceleme kesiti gereklidir.");
        var declared = metadata.GetProperty("files").EnumerateArray().ToArray();
        if (declared.Length != Files.Length) throw new InvalidOperationException("Kesit dört kaynak dosyasını içermelidir.");

        // Dosyalar doğrulamadan son okumaya kadar yazma paylaşımı olmadan açık tutulur.
        var streams = new Dictionary<string, FileStream>();
        try
        {
            foreach (var file in Files)
            {
                var entries = declared.Where(x => Text(x, "FileName") == file.File && Text(x, "EntityCode") == file.Entity).ToArray();
                if (entries.Length != 1) throw new InvalidOperationException("Kesit dosya tanımları eksik veya yinelenmiş.");
                var stream = new FileStream(Path.Combine(directory, file.File), FileMode.Open, FileAccess.Read, FileShare.Read,
                    64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
                streams.Add(file.Entity, stream);
                var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream));
                if (!string.Equals(hash, Text(entries[0], "Sha256"), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"{file.Entity} dosyasının hash doğrulaması başarısız.");
                stream.Position = 0;
                long count = 0;
                using (var reader = Reader(stream)) while (await reader.ReadLineAsync() is not null) count++;
                if (count != entries[0].GetProperty("Count").GetInt64() || count == 0)
                    throw new InvalidOperationException($"{file.Entity} kayıt sayısı manifestle uyuşmuyor.");
                stream.Position = 0;
            }

            var customers = await LoadAsync(streams["Customer"], "CustomerID");
            var contracts = await LoadAsync(streams["Contract"], "ContractID");
            var histories = await LoadAsync(streams["ContractHistory"], "ContractHistoryID");
            var historyGroups = histories.Values.ToLookup(x => Id(x, "ContractID"));

            using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath), new JsonDocumentOptions
            { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            var connection = new SqlConnectionStringBuilder(settings.RootElement.GetProperty("AppSettings")
                .GetProperty("MSSQLConnectionString").GetString()) { ApplicationIntent = ApplicationIntent.ReadOnly };
            if (connection.DataSource != "192.168.1.8" || connection.InitialCatalog != "AssistFlowTest")
                throw new InvalidOperationException("Ödeme incelemesi yalnız AssistFlowTest üzerinden yapılabilir.");
            await using var ownedDb = transactionContext is null ? new AppDataContext(new DbContextOptionsBuilder<AppDataContext>()
                .UseSqlServer(connection.ConnectionString, sql => sql.CommandTimeout(180)).Options) : null;
            var db = transactionContext ?? ownedDb!;
            db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
            // Tutarlı hedef okuması kısa tutulur; dosya analizi sırasında SQL kilidi tutulmaz.
            if (transactionContext is not null && db.Database.CurrentTransaction is null)
                throw new InvalidOperationException("Aktarım doğrulaması açık transaction gerektirir.");
            await using var transaction = transactionContext is null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable) : null;
            var batch = await db.Set<CollectionMigrationBatch>().SingleAsync(x => x.Id == batchId && x.SourceSystem == "MGS");
            var stages = await db.Set<CollectionMigrationContractStage>().Include(x => x.SourceRow)
                .Where(x => x.SourceRow.BatchId == batchId).ToListAsync();
            var rateStages = await db.Set<CollectionMigrationRatePeriodStage>().Include(x => x.SourceRow)
                .Where(x => x.SourceRow.BatchId == batchId).ToListAsync();
            var maps = await db.Set<CollectionMigrationMap>().Where(x => x.SourceSystem == "MGS").ToListAsync();
            var targets = await db.Set<CollectionContract>().ToDictionaryAsync(x => x.Id);
            var targetRates = await db.Set<CollectionContractRatePeriod>().ToDictionaryAsync(x => x.Id);
            var targetCustomers = await db.Customers.Select(x => new { x.Id, x.IsDeleted, x.SubscriberCode,
                GroupCode = x.CustomerGroup == null ? null : x.CustomerGroup.Code,
                TypeCode = x.CustomerType == null ? null : x.CustomerType.Code }).ToDictionaryAsync(x => x.Id);
            var currencies = await db.CurrencyTypes.Select(x => x.Id).ToHashSetAsync();
            var frequencies = await db.Set<CollectionPaymentFrequency>().ToDictionaryAsync(x => x.Id, x => (int)x.IntervalMonths);
            var currencyMaps = await db.Set<CollectionMigrationReferenceMap>()
                .Where(x => x.BatchId == batchId && x.ReferenceKind == "CurrencyType" && x.Status == CollectionMigrationDecisionStatus.Accepted)
                .ToListAsync();
            var paymentCount = await db.Set<CollectionPayment>().LongCountAsync();
            var importedRequests = await db.Set<CollectionPaymentOperation>()
                .Where(x => x.ActorUserId == 0 && x.Kind == CollectionPaymentOperationKind.Create)
                .Select(x => x.RequestId).ToHashSetAsync();
            if (transaction is not null) await transaction.CommitAsync();

            var stageByContract = stages.ToDictionary(x => ParseId(x.SourceRow.SourceId));
            var ratesByContract = rateStages.ToLookup(x => ParseId(x.SourceContractId));
            var contractMaps = maps.Where(x => x.EntityCode == "Contract").ToDictionary(x => ParseId(x.SourceId));
            var rateMaps = maps.Where(x => x.EntityCode == "ContractHistory").ToDictionary(x => ParseId(x.SourceId));
            var paymentMaps = maps.Where(x => x.EntityCode == "Payment").ToDictionary(x => ParseId(x.SourceId));
            var approvedCurrencies = currencyMaps.ToDictionary(x => ParseId(x.SourceId), x => x.TargetCurrencyTypeId);
            var targetRateCounts = targetRates.Values.Where(x => !x.IsDeleted).GroupBy(x => x.ContractId)
                .ToDictionary(x => x.Key, x => x.Count());
            var sourceChecks = new Dictionary<long, string[]>();
            var candidateCache = new Dictionary<(long Contract, DateOnly Period), PaymentCurrencyCandidateResult>();

            string[] CheckContract(long contractId, CollectionMigrationContractStage stage, CollectionContract target)
            {
                if (sourceChecks.TryGetValue(contractId, out var cached)) return cached;
                var errors = new HashSet<string>();
                var source = contracts[contractId];
                if (!SHA256.HashData(Encoding.UTF8.GetBytes(source.GetRawText())).SequenceEqual(stage.SourceRow.PayloadHash))
                    errors.Add("SOURCE_CONTRACT_CHANGED");
                if (ParseId(stage.SourceCustomerId) != Id(source, "CustomerID")) errors.Add("SOURCE_CUSTOMER_CHANGED");
                if (stage.TargetCustomerId != target.CustomerId) errors.Add("TARGET_CUSTOMER_MISMATCH");
                if (target.EndDate != stage.EndDate || stage.StartingYear is not (>= 1 and < 9999)
                    || stage.StartingMonth is not (>= 1 and <= 12)
                    || target.StartDate != new DateOnly(stage.StartingYear.Value, stage.StartingMonth.Value, 1))
                    errors.Add("TARGET_CONTRACT_CHANGED");
                if (!targetCustomers.TryGetValue(target.CustomerId, out var customer) || customer.IsDeleted)
                    errors.Add("TARGET_CUSTOMER_MISSING");
                else
                {
                    if (CollectionCustomerClassification.Issue(customer.GroupCode, customer.TypeCode) is not null)
                        errors.Add("TARGET_CUSTOMER_OUTSIDE_SCOPE");
                    if (string.IsNullOrWhiteSpace(customer.SubscriberCode) || customers.TryGetValue(Id(source, "CustomerID"), out var sourceCustomer)
                        && CollectionCustomerClassification.Normalize(Text(sourceCustomer, "SubscriberNo")) !=
                        CollectionCustomerClassification.Normalize(customer.SubscriberCode)) errors.Add("SUBSCRIBER_CHANGED");
                }

                var sourceRates = historyGroups[contractId].ToDictionary(x => Id(x, "ContractHistoryID"));
                var stagedRates = ratesByContract[contractId].ToArray();
                if (!sourceRates.Keys.ToHashSet().SetEquals(stagedRates.Select(x => ParseId(x.SourceRow.SourceId))))
                    errors.Add("SOURCE_HISTORY_SET_CHANGED");
                if (targetRateCounts.GetValueOrDefault(target.Id) != stagedRates.Length)
                    errors.Add("TARGET_HISTORY_SET_CHANGED");
                foreach (var rate in stagedRates)
                {
                    var rateId = ParseId(rate.SourceRow.SourceId);
                    if (!sourceRates.TryGetValue(rateId, out var sourceRate) ||
                        !SHA256.HashData(Encoding.UTF8.GetBytes(sourceRate.GetRawText())).SequenceEqual(rate.SourceRow.PayloadHash))
                        errors.Add("SOURCE_HISTORY_CHANGED");
                    if (rate.Status != CollectionMigrationRowStatus.Applied || !rateMaps.TryGetValue(rateId, out var map)
                        || map.TargetRatePeriodId is not { } targetId || !targetRates.TryGetValue(targetId, out var targetRate)
                        || targetRate.IsDeleted || targetRate.ContractId != target.Id)
                    { errors.Add("RATE_MAP_MISSING"); continue; }
                    var sourceFrequency = LegacyPaymentFrequencyRules.Resolve(
                        ParseId(rate.SourcePaymentTypeId) is > 0 and <= int.MaxValue ? (int)ParseId(rate.SourcePaymentTypeId) : null);
                    if (rate.EffectiveFrom is null || rate.EffectiveToExclusive <= rate.EffectiveFrom || rate.BillingBehavior is null
                        || sourceFrequency is null || rate.TargetPaymentFrequencyId is not { } frequency
                        || !frequencies.TryGetValue(frequency, out var interval) || interval != sourceFrequency.IntervalMonths)
                        errors.Add("RATE_DEFINITION_INVALID");
                    if (targetRate.EffectiveFrom != rate.EffectiveFrom || targetRate.EffectiveToExclusive != rate.EffectiveToExclusive
                        || targetRate.Amount != rate.Amount || targetRate.CurrencyTypeId != rate.TargetCurrencyTypeId
                        || targetRate.PaymentFrequencyId != rate.TargetPaymentFrequencyId || targetRate.BillingBehavior != rate.BillingBehavior
                        || targetRate.BillingAnchor != rate.EffectiveFrom || targetRate.OriginalAnchorDay is not null)
                        errors.Add("TARGET_RATE_CHANGED");
                }
                return sourceChecks[contractId] = errors.Order().ToArray();
            }

            PaymentCurrencyCandidateResult Candidates(long contractId, DateOnly period)
            {
                if (candidateCache.TryGetValue((contractId, period), out var cached)) return cached;
                var candidates = new List<PaymentCurrencyCandidate>();
                foreach (var rate in ratesByContract[contractId])
                {
                    if (rate.BillingBehavior != CollectionBillingBehavior.Billable || rate.EffectiveFrom is not { } from
                        || rate.EffectiveToExclusive <= from || rate.TargetPaymentFrequencyId is not { } frequency
                        || !frequencies.TryGetValue(frequency, out var months) || !CollectionPeriodRules.IsSupportedInterval(months)) continue;
                    if (!CollectionPeriodRules.GetDueDates(from, rate.EffectiveToExclusive, months, period, period.AddMonths(1)).Any()) continue;
                    var currency = approvedCurrencies.GetValueOrDefault(ParseId(rate.SourceCurrencyId));
                    int? mappedCurrency = currency.HasValue && currency == rate.TargetCurrencyTypeId && currencies.Contains(currency.Value)
                        && currency.Value is > 0 and <= int.MaxValue ? (int)currency.Value : null;
                    candidates.Add(new(ParseId(rate.SourceRow.SourceId), mappedCurrency));
                }
                return candidateCache[(contractId, period)] = PaymentCurrencyCandidateRules.Evaluate(candidates);
            }

            var output = Path.Combine(directory, $"review-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(output);
            var scopes = new SortedDictionary<string, long>();
            var issues = new SortedDictionary<string, long>();
            var retainedIssues = new SortedDictionary<string, long>();
            var currencyStatuses = new SortedDictionary<string, long>();
            var totals = new SortedDictionary<int, (long Count, decimal Amount)>();
            var paymentIds = new HashSet<long>();
            long reviewed = 0, preliminary = 0;
            await using (var report = new StreamWriter(new FileStream(Path.Combine(output, "payments.ndjson"), FileMode.CreateNew), new UTF8Encoding(false)))
            using (var reader = Reader(streams["Payment"]))
            {
                while (await reader.ReadLineAsync() is { } line)
                {
                    using var document = JsonDocument.Parse(line);
                    var row = document.RootElement;
                    var paymentId = Id(row, "PaymentID");
                    if (paymentId <= 0 || !paymentIds.Add(paymentId))
                        throw new InvalidOperationException("Ödeme kimliği geçersiz veya mükerrer; rapor tamamlanmadı.");
                    var contractId = Id(row, "ContractID");
                    var customerId = Id(row, "CustomerID");
                    var errors = new HashSet<string>();
                    if (!customers.ContainsKey(customerId)) errors.Add("SOURCE_CUSTOMER_MISSING");
                    var sourceExists = contracts.TryGetValue(contractId, out var contract);
                    if (sourceExists && customerId != Id(contract, "CustomerID")) errors.Add("PAYMENT_CUSTOMER_MISMATCH");
                    stageByContract.TryGetValue(contractId, out var stage);
                    contractMaps.TryGetValue(contractId, out var map);
                    var target = map?.TargetContractId is { } targetId ? targets.GetValueOrDefault(targetId) : null;
                    var scope = !sourceExists ? "MissingLegacyContract"
                        : stage?.Status == CollectionMigrationRowStatus.Excluded ? "ExcludedContract"
                        : stage?.Status == CollectionMigrationRowStatus.Applied
                            ? target is { IsDeleted: false } ? "RetainedMappedContract" : "AppliedTargetMismatch"
                        : stage is null ? "OutsideContractSnapshot" : "WaitingContract";
                    var period = Month(row, "PeriodYear", "PeriodMonth");
                    if (period is null) errors.Add("INVALID_PERIOD");
                    var paymentDateText = Text(row, "Date");
                    DateOnly? paymentDate = DateTime.TryParseExact(paymentDateText, "O", CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind, out var date) ? DateOnly.FromDateTime(date) : null;
                    if (paymentDate is null) errors.Add("INVALID_PAYMENT_DATE");
                    var amount = Number(row, "Amount");
                    if (amount is null or < -9999999999999999.99m or > 9999999999999999.99m || decimal.Round(amount.Value, 2) != amount)
                        errors.Add("INVALID_AMOUNT");
                    else if (amount < 0) errors.Add("NEGATIVE_AMOUNT_REVIEW");
                    else if (amount == 0) errors.Add("ZERO_AMOUNT_REVIEW");
                    bool? free = Text(row, "Free")?.Trim() switch { "Evet" => true, "Hayır" => false, _ => null };
                    if (free is null) errors.Add("UNKNOWN_FREE_VALUE");
                    else if (free.Value) errors.Add("FREE_PAYMENT_REVIEW");
                    if (Text(row, "Description")?.Length > 1000) errors.Add("DESCRIPTION_TOO_LONG");
                    if (paymentMaps.ContainsKey(paymentId)) errors.Add("PAYMENT_ALREADY_MAPPED");
                    var previouslyImported = importedRequests.Contains(CollectionPaymentImportIdentity.For(paymentId));
                    if (previouslyImported && transactionContext is null) errors.Add("PAYMENT_ALREADY_IMPORTED");
                    PaymentCurrencyCandidateResult? currency = null;
                    if (scope == "RetainedMappedContract")
                    {
                        errors.UnionWith(CheckContract(contractId, stage!, target!));
                        if (period is not null)
                        {
                            currency = Candidates(contractId, period.Value);
                            Increment(currencyStatuses, currency.Status.ToString());
                            if (currency.Status != PaymentCurrencyCandidateStatus.SingleRateCurrency)
                                errors.Add($"CURRENCY_{currency.Status}");
                        }
                    }
                    // Tek aday da legacy view mutabakatı ve silme uyumu bitmeden uygulamaya hazır değildir.
                    var isPreliminary = scope == "RetainedMappedContract" && errors.Count == 0;
                    if (isPreliminary)
                    {
                        preliminary++;
                        var key = currency!.ResolvedCurrencyTypeId!.Value;
                        var previous = totals.GetValueOrDefault(key);
                        totals[key] = (previous.Count + 1, previous.Amount + amount!.Value);
                    }
                    Increment(scopes, scope);
                    foreach (var error in errors)
                    {
                        Increment(issues, error);
                        if (scope == "RetainedMappedContract") Increment(retainedIssues, error);
                    }
                    await report.WriteLineAsync(JsonSerializer.Serialize(new
                    {
                        PaymentId = paymentId, SourceContractId = contractId, SourceCustomerId = customerId,
                        TargetContractId = target?.Id, Period = period, PaymentDate = paymentDate, Amount = amount, IsFree = free,
                        SourcePayloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(line))),
                        Scope = scope, Issues = errors.Order().ToArray(), Currency = currency,
                        PreviouslyImported = previouslyImported, PreliminaryCandidate = isPreliminary, ReadyToApply = false
                    }));
                    reviewed++;
                }
            }
            string reportHash;
            await using (var reportFile = File.OpenRead(Path.Combine(output, "payments.ndjson")))
                reportHash = Convert.ToHexString(await SHA256.HashDataAsync(reportFile));
            var summary = new
            {
                ReviewVersion = "payment-review-v1", ReviewedAtUtc = DateTimeOffset.UtcNow,
                SnapshotKey = Text(metadata, "snapshotKey"), ManifestHash = manifestHash,
                ContractBatchId = batch.Id, ContractManifestHash = Convert.ToHexString(batch.ManifestHash),
                TargetPaymentCount = paymentCount, PaymentCount = reviewed, ScopeCounts = scopes, IssueCounts = issues,
                RetainedIssueCounts = retainedIssues, PaymentReportHash = reportHash,
                RetainedNeedsReview = scopes.GetValueOrDefault("RetainedMappedContract") - preliminary,
                RetainedCurrencyStatuses = currencyStatuses, PreliminaryCandidates = preliminary, ReadyToApply = false,
                PreliminaryTotals = totals.Select(x => new { CurrencyTypeId = x.Key, x.Value.Count, x.Value.Amount }),
                RemainingGates = new[] { "Legacy view karşılaştırması", "Fiziksel silme / kalıcı aktarım kimliği uyumu", "Uygulama öncesi güncel hedef mutabakatı" }
            };
            await File.WriteAllTextAsync(Path.Combine(output, "summary.json"), JsonSerializer.Serialize(summary, JsonOptions));
            Console.WriteLine($"Ödeme incelemesi tamamlandı: {reviewed} kayıt; ön kontrol adayı {preliminary}. Hedefe yazılan ödeme 0.");
            Console.WriteLine($"Rapor: {output}");
            return output;
        }
        finally { foreach (var stream in streams.Values) await stream.DisposeAsync(); }
    }

    private static StreamReader Reader(Stream stream) => new(stream, new UTF8Encoding(false, true), true, 64 * 1024, leaveOpen: true);
    private static async Task<Dictionary<long, JsonElement>> LoadAsync(Stream stream, string key)
    {
        var rows = new Dictionary<long, JsonElement>();
        using var reader = Reader(stream);
        while (await reader.ReadLineAsync() is { } line)
        {
            using var document = JsonDocument.Parse(line);
            var id = Id(document.RootElement, key);
            if (id <= 0 || !rows.TryAdd(id, document.RootElement.Clone()))
                throw new InvalidOperationException($"{key} geçersiz veya mükerrer; inceleme durduruldu.");
        }
        return rows;
    }
    private static string? Text(JsonElement row, string key) => row.TryGetProperty(key, out var value)
        && value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined) ? value.ToString() : null;
    private static long ParseId(string? value) => long.TryParse(value?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0 ? id : 0;
    private static long Id(JsonElement row, string key) => ParseId(Text(row, key));
    private static decimal? Number(JsonElement row, string key) => decimal.TryParse(Text(row, key),
        NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount) ? amount : null;
    private static DateOnly? Month(JsonElement row, string yearKey, string monthKey)
    {
        var year = Id(row, yearKey);
        var month = Id(row, monthKey);
        return year is >= 1 and < 9999 && month is >= 1 and <= 12 ? new DateOnly((int)year, (int)month, 1) : null;
    }
    private static void Increment(IDictionary<string, long> counts, string key) => counts[key] = counts.TryGetValue(key, out var count) ? count + 1 : 1;
}
