using System.Globalization;
using System.Text.Json;
using Data.Concrete.EfCore.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Model.Concrete;
using Model.Concrete.Collections;

internal static class CollectionStageValidator
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly HashSet<string> OwnedCodes =
    [
        "CUSTOMER_ORPHAN", "SUBSCRIBER_BLANK", "SUBSCRIBER_TOO_LONG", "SUBSCRIBER_SOURCE_DUPLICATE",
        "CUSTOMER_TARGET_MISSING", "CUSTOMER_TARGET_AMBIGUOUS", "CUSTOMER_TARGET_DELETED",
        "DATE_INVALID", "RANGE_REVERSED", "CONTRACT_PARENT_MISSING", "HISTORY_CUSTOMER_MISMATCH",
        "PERIOD_START_DUPLICATE", "PERIOD_OVERLAP", "MULTIPLE_OPEN_PERIODS", "HISTORY_MISSING", "HISTORY_INVALID",
        "SERVICE_TYPE_MAP_MISSING", "CURRENCY_MAP_MISSING", "CUSTOMER_TYPE_MAP_MISSING",
        "CUSTOMER_TYPE_MISMATCH", "CONTRACT_STATUS_MAP_MISSING", "SUBSCRIPTION_STATUS_MAP_MISSING",
        "PAYMENT_METHOD_MAP_MISSING", "PAYMENT_FREQUENCY_MAP_MISSING", "AMOUNT_INVALID",
        "CONTRACT_NOT_INCLUDED", "SUBSCRIPTION_STATUS_UNKNOWN", "PROCESS_TYPE_UNKNOWN",
        "CONTRACT_CURRENT_RATE_MISMATCH", "ATTACHMENT_METADATA_TOO_LONG", "ATTACHMENT_METADATA_INCOMPLETE"
    ];

    public static async Task RunAsync(string sourceSystem, string snapshotKey, byte[] expectedHash,
        int expectedCustomers, int expectedContracts, int expectedHistories, string settingsPath)
    {
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath), new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        });
        var connection = new SqlConnectionStringBuilder(settings.RootElement.GetProperty("AppSettings")
            .GetProperty("MSSQLConnectionString").GetString());
        if (connection.DataSource != "192.168.1.8" || connection.InitialCatalog != "AssistFlowTest")
            throw new InvalidOperationException("Staging doğrulaması yalnız AssistFlowTest üzerinde çalışır.");
        var options = new DbContextOptionsBuilder<AppDataContext>()
            .UseSqlServer(connection.ConnectionString, sql =>
            {
                sql.EnableRetryOnFailure();
                sql.CommandTimeout(180);
            }).Options;
        await using var db = new AppDataContext(options);
        var batch = await db.Set<CollectionMigrationBatch>().SingleOrDefaultAsync(x =>
            x.SourceSystem == sourceSystem && x.SnapshotKey == snapshotKey)
            ?? throw new InvalidOperationException("Kesit staging kaydı bulunamadı.");
        if (!batch.ManifestHash.SequenceEqual(expectedHash))
            throw new InvalidOperationException("Staging manifest hash'i verilen kesitle uyuşmuyor.");
        if (batch.Status is not (CollectionMigrationBatchStatus.Staged or CollectionMigrationBatchStatus.Validating
            or CollectionMigrationBatchStatus.NeedsReview))
            throw new InvalidOperationException("Bu durumdaki kesit yeniden doğrulanamaz.");

        var rows = await db.Set<CollectionMigrationSourceRow>().AsNoTracking()
            .Where(x => x.BatchId == batch.Id)
            .Select(x => new SourceRow(x.Id, x.EntityCode, x.SourceId, x.SourceParentId, x.Payload))
            .ToListAsync();
        var sourceCustomers = rows.Where(x => x.EntityCode == "Customer").ToList();
        var contracts = rows.Where(x => x.EntityCode == "Contract").ToList();
        var histories = rows.Where(x => x.EntityCode == "ContractHistory").ToList();
        if (sourceCustomers.Count != expectedCustomers || contracts.Count != expectedContracts
            || histories.Count != expectedHistories)
            throw new InvalidOperationException("Müşteri, sözleşme veya tarihçe staging satırları kesitle uyuşmuyor.");
        if (await db.Set<CollectionMigrationContractStage>().CountAsync(x => x.SourceRow.BatchId == batch.Id) != contracts.Count
            || await db.Set<CollectionMigrationRatePeriodStage>().CountAsync(x => x.SourceRow.BatchId == batch.Id) != histories.Count)
            throw new InvalidOperationException("Staging izdüşümleri ham satır sayısıyla uyuşmuyor.");
        if (await db.Set<CollectionMigrationContractStage>().AnyAsync(x => x.SourceRow.BatchId == batch.Id
                && (x.Status == CollectionMigrationRowStatus.Applied || x.Status == CollectionMigrationRowStatus.Excluded))
            || await db.Set<CollectionMigrationRatePeriodStage>().AnyAsync(x => x.SourceRow.BatchId == batch.Id
                && (x.Status == CollectionMigrationRowStatus.Applied || x.Status == CollectionMigrationRowStatus.Excluded)))
            throw new InvalidOperationException("Yüklenmiş veya hariç tutulmuş satırlar yeniden doğrulanmaz.");

        var acceptedMaps = await db.Set<CollectionMigrationReferenceMap>().AsNoTracking()
            .Where(x => x.BatchId == batch.Id && x.Status == CollectionMigrationDecisionStatus.Accepted)
            .ToListAsync();
        var duplicateMap = acceptedMaps.GroupBy(x => (x.ReferenceKind, SourceId: NormalizeId(x.SourceId)))
            .FirstOrDefault(x => x.Count() > 1);
        if (duplicateMap is not null)
            throw new InvalidOperationException("Aynı kaynak kimliğinin birden fazla onaylı referans eşlemesi var.");
        var referenceMaps = acceptedMaps.ToDictionary(x => (x.ReferenceKind, NormalizeId(x.SourceId)));
        var activeServiceTypes = await db.Set<ServiceType>().AsNoTracking()
            .Where(x => !x.IsDeleted).Select(x => x.Id).ToHashSetAsync();
        var includedContractStatuses = await db.Set<CollectionContractStatus>().AsNoTracking()
            .Where(x => x.Code == "EXISTS").Select(x => x.Id).ToHashSetAsync();
        var knownSubscriptionStatuses = await db.Set<CollectionSubscriptionStatus>().AsNoTracking()
            .Where(x => x.Code == "ACTIVE" || x.Code == "FROZEN").Select(x => x.Id).ToHashSetAsync();
        long? Mapped(string kind, string? sourceId) => sourceId is null ? null
            : referenceMaps.TryGetValue((kind, NormalizeId(sourceId)), out var map) ? kind switch
            {
                "ServiceType" => map.TargetServiceTypeId,
                "CurrencyType" => map.TargetCurrencyTypeId,
                "CustomerType" => map.TargetCustomerTypeId,
                "ContractStatus" => map.TargetContractStatusId,
                "SubscriptionStatus" => map.TargetSubscriptionStatusId,
                "PaymentMethod" => map.TargetPaymentMethodId,
                "PaymentFrequency" => map.TargetPaymentFrequencyId,
                _ => null
            } : null;

        batch.Status = CollectionMigrationBatchStatus.Validating;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var customerCodes = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var customerById = new Dictionary<string, string?>(StringComparer.Ordinal);
        var customerRawById = new Dictionary<string, string?>(StringComparer.Ordinal);
        var customerTypeById = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var row in sourceCustomers)
        {
            using var document = JsonDocument.Parse(row.Payload);
            var raw = Text(document.RootElement, "SubscriberNo");
            var code = NormalizeCode(raw);
            var key = NormalizeId(row.SourceId);
            customerById[key] = code;
            customerRawById[key] = raw;
            customerTypeById[key] = Text(document.RootElement, "Type");
            if (code is null) continue;
            if (!customerCodes.TryGetValue(code, out var ids)) customerCodes[code] = ids = [];
            ids.Add(row.SourceId);
        }
        var targetCodes = new Dictionary<string, List<TargetCustomer>>(StringComparer.Ordinal);
        var targetCustomers = await db.Set<Customer>().AsNoTracking()
            .Select(x => new TargetCustomer(x.Id, x.SubscriberCode, x.IsDeleted, x.CustomerTypeId)).ToListAsync();
        var targetById = targetCustomers.ToDictionary(x => x.Id);
        foreach (var target in targetCustomers)
        {
            var code = NormalizeCode(target.Code);
            if (code is null) continue;
            if (!targetCodes.TryGetValue(code, out var values)) targetCodes[code] = values = [];
            values.Add(target);
        }

        var contractById = contracts.ToDictionary(x => NormalizeId(x.SourceId), StringComparer.Ordinal);
        var historyByParent = histories.GroupBy(x => NormalizeId(x.SourceParentId))
            .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.Ordinal);
        var problems = new Dictionary<long, HashSet<string>>();
        var resolvedCustomers = new Dictionary<long, (string? Raw, string? Normalized, long? TargetId)>();
        var resolvedServices = new Dictionary<long, long?>();
        var contractDates = new Dictionary<long, (short? Month, short? Year, DateOnly? EndDate)>();
        var currentRates = new Dictionary<string, (decimal? Amount, long? Currency, long? Frequency)>(StringComparer.Ordinal);
        foreach (var row in contracts)
        {
            using var document = JsonDocument.Parse(row.Payload);
            var root = document.RootElement;
            var customerId = NormalizeId(Text(root, "CustomerID"));
            if (!customerById.TryGetValue(customerId, out var code)) Add(row.Id, "CUSTOMER_ORPHAN");
            else if (code is null) Add(row.Id, "SUBSCRIBER_BLANK");
            else if (customerCodes[code].Count > 1) Add(row.Id, "SUBSCRIBER_SOURCE_DUPLICATE");
            else if (!targetCodes.TryGetValue(code, out var targets)) Add(row.Id, "CUSTOMER_TARGET_MISSING");
            else if (targets.Count != 1) Add(row.Id, "CUSTOMER_TARGET_AMBIGUOUS");
            else if (targets[0].IsDeleted) Add(row.Id, "CUSTOMER_TARGET_DELETED");
            var targetId = code is not null && customerCodes.TryGetValue(code, out var sourceIds) && sourceIds.Count == 1
                && targetCodes.TryGetValue(code, out var targetIds) && targetIds.Count == 1 && !targetIds[0].IsDeleted
                ? targetIds[0].Id : (long?)null;
            customerRawById.TryGetValue(customerId, out var raw);
            if (raw?.Length > 200 || code?.Length > 200)
            {
                Add(row.Id, "SUBSCRIBER_TOO_LONG");
                targetId = null;
            }
            resolvedCustomers[row.Id] = (raw?.Length <= 200 ? raw : null, code?.Length <= 200 ? code : null, targetId);
            var service = Mapped("ServiceType", Text(root, "ServiceTypeID"));
            resolvedServices[row.Id] = service;
            if (service is null || !activeServiceTypes.Contains(service.Value)) Add(row.Id, "SERVICE_TYPE_MAP_MISSING");
            if (customerTypeById.TryGetValue(customerId, out var sourceCustomerType))
            {
                var mappedType = Mapped("CustomerType", sourceCustomerType);
                if (mappedType is null) Add(row.Id, "CUSTOMER_TYPE_MAP_MISSING");
                else if (targetId.HasValue && targetById[targetId.Value].CustomerTypeId != mappedType)
                    Add(row.Id, "CUSTOMER_TYPE_MISMATCH");
            }
            var sourceContractStatus = Text(root, "ContractStatusID");
            var mappedContractStatus = Mapped("ContractStatus", sourceContractStatus);
            if (!string.IsNullOrWhiteSpace(sourceContractStatus) && mappedContractStatus is null)
                Add(row.Id, "CONTRACT_STATUS_MAP_MISSING");
            if (mappedContractStatus is null || !includedContractStatuses.Contains(mappedContractStatus.Value))
                Add(row.Id, "CONTRACT_NOT_INCLUDED");
            var sourceSubscriptionStatus = Text(root, "SubscriptionStatusID");
            var mappedSubscriptionStatus = Mapped("SubscriptionStatus", sourceSubscriptionStatus);
            if (!string.IsNullOrWhiteSpace(sourceSubscriptionStatus) && mappedSubscriptionStatus is null)
                Add(row.Id, "SUBSCRIPTION_STATUS_MAP_MISSING");
            if (mappedSubscriptionStatus is null || !knownSubscriptionStatuses.Contains(mappedSubscriptionStatus.Value))
                Add(row.Id, "SUBSCRIPTION_STATUS_UNKNOWN");
            if (!string.IsNullOrWhiteSpace(Text(root, "PaymentMethodID"))
                && Mapped("PaymentMethod", Text(root, "PaymentMethodID")) is null)
                Add(row.Id, "PAYMENT_METHOD_MAP_MISSING");
            var startValid = TryMonth(root, "StartingDateYear", "StartingDateMonth", out var start);
            var endValid = TryEndDate(root, out var endExclusive);
            if (!startValid || !endValid) Add(row.Id, "DATE_INVALID");
            else if (endExclusive is not null && endExclusive <= start) Add(row.Id, "RANGE_REVERSED");
            contractDates[row.Id] = (startValid ? (short)start.Month : null, startValid ? (short)start.Year : null,
                endValid ? endExclusive?.AddDays(-1) : null);
            if (!historyByParent.ContainsKey(NormalizeId(row.SourceId))) Add(row.Id, "HISTORY_MISSING");
            var attachmentName = Text(root, "FileAttachmentName");
            var attachmentPath = Text(root, "FileAttachmentPath");
            if (attachmentName?.Length > 255 || attachmentPath?.Length > 1000)
                Add(row.Id, "ATTACHMENT_METADATA_TOO_LONG");
            if (string.IsNullOrWhiteSpace(attachmentName) != string.IsNullOrWhiteSpace(attachmentPath))
                Add(row.Id, "ATTACHMENT_METADATA_INCOMPLETE");
            currentRates[NormalizeId(row.SourceId)] = (ParseAmount(root),
                Mapped("CurrencyType", Text(root, "CurrencyID")),
                Mapped("PaymentFrequency", Text(root, "PaymentTypeID")));
        }

        var historyRanges = new Dictionary<string, List<HistoryRange>>(StringComparer.Ordinal);
        var parsedHistory = new Dictionary<long, (DateOnly? From, DateOnly? ToExclusive)>();
        var resolvedCurrencies = new Dictionary<long, long?>();
        var resolvedFrequencies = new Dictionary<long, long?>();
        var resolvedAmounts = new Dictionary<long, decimal?>();
        var resolvedBehaviors = new Dictionary<long, CollectionBillingBehavior?>();
        foreach (var row in histories)
        {
            using var document = JsonDocument.Parse(row.Payload);
            var root = document.RootElement;
            var parentKey = NormalizeId(row.SourceParentId);
            var fromValid = TryMonth(root, "StartingDateYear", "StartingDateMonth", out var from);
            var toValid = TryEndDate(root, out var toExclusive);
            if (!fromValid || !toValid) Add(row.Id, "DATE_INVALID");
            else if (toExclusive is not null && toExclusive <= from) Add(row.Id, "RANGE_REVERSED");
            parsedHistory[row.Id] = (fromValid ? from : null, toValid ? toExclusive : null);
            var currency = Mapped("CurrencyType", Text(root, "CurrencyID"));
            resolvedCurrencies[row.Id] = currency;
            if (currency is null) Add(row.Id, "CURRENCY_MAP_MISSING");
            var frequency = Mapped("PaymentFrequency", Text(root, "PaymentTypeID"));
            resolvedFrequencies[row.Id] = frequency;
            if (frequency is null) Add(row.Id, "PAYMENT_FREQUENCY_MAP_MISSING");
            var amount = ParseAmount(root);
            resolvedAmounts[row.Id] = amount;
            if (amount is null) Add(row.Id, "AMOUNT_INVALID");
            var behavior = ParseBehavior(Text(root, "ProcessType"));
            resolvedBehaviors[row.Id] = behavior;
            if (behavior is null) Add(row.Id, "PROCESS_TYPE_UNKNOWN");
            if (!contractById.TryGetValue(parentKey, out var contract)) Add(row.Id, "CONTRACT_PARENT_MISSING");
            else
            {
                using var contractDocument = JsonDocument.Parse(contract.Payload);
                if (NormalizeId(Text(root, "CustomerID")) != NormalizeId(Text(contractDocument.RootElement, "CustomerID")))
                    Add(row.Id, "HISTORY_CUSTOMER_MISMATCH");
            }
            if (fromValid && toValid)
            {
                if (!historyRanges.TryGetValue(parentKey, out var list)) historyRanges[parentKey] = list = [];
                list.Add(new(row.Id, from, toExclusive));
            }
        }
        foreach (var ranges in historyRanges.Values)
        {
            foreach (var sameStart in ranges.GroupBy(x => x.From).Where(x => x.Count() > 1))
                foreach (var item in sameStart) Add(item.RowId, "PERIOD_START_DUPLICATE");
            var open = ranges.Where(x => x.ToExclusive is null).ToList();
            if (open.Count > 1) foreach (var item in open) Add(item.RowId, "MULTIPLE_OPEN_PERIODS");
            var ordered = ranges.OrderBy(x => x.From).ThenBy(x => x.RowId).ToList();
            var maxEnd = DateOnly.MinValue;
            long? maxRow = null;
            foreach (var item in ordered)
            {
                if (item.From < maxEnd)
                {
                    Add(item.RowId, "PERIOD_OVERLAP");
                    if (maxRow.HasValue) Add(maxRow.Value, "PERIOD_OVERLAP");
                }
                var end = item.ToExclusive ?? DateOnly.MaxValue;
                if (end > maxEnd) { maxEnd = end; maxRow = item.RowId; }
            }
        }
        foreach (var row in contracts)
        {
            if (historyByParent.TryGetValue(NormalizeId(row.SourceId), out var related)
                && related.Any(x => problems.ContainsKey(x.Id))) Add(row.Id, "HISTORY_INVALID");
            if (historyByParent.TryGetValue(NormalizeId(row.SourceId), out related))
            {
                var openHistory = related.Where(x => parsedHistory[x.Id].ToExclusive is null).ToList();
                if (openHistory.Count == 1)
                {
                    var latest = openHistory[0];
                    var current = currentRates[NormalizeId(row.SourceId)];
                    if (current.Amount is null || current.Currency is null || current.Frequency is null
                        || current.Amount != resolvedAmounts[latest.Id]
                        || current.Currency != resolvedCurrencies[latest.Id]
                        || current.Frequency != resolvedFrequencies[latest.Id])
                        Add(row.Id, "CONTRACT_CURRENT_RATE_MISMATCH");
                }
            }
        }

        // Open validator issues are a replaceable projection of the current snapshot and rule version.
        // Resolved history and issue codes owned by other/manual processes are preserved.
        foreach (var codes in OwnedCodes.Chunk(20))
            await db.Set<CollectionMigrationIssue>()
                .Where(x => x.SourceRow.BatchId == batch.Id
                    && x.Status == CollectionMigrationIssueStatus.Open && codes.Contains(x.IssueCode))
                .ExecuteDeleteAsync();
        var pendingIssueCount = 0;
        foreach (var (rowId, codes) in problems)
            foreach (var code in codes)
                if (OwnedCodes.Contains(code))
                {
                    db.Set<CollectionMigrationIssue>().Add(new()
                    {
                        SourceRowId = rowId, IssueCode = code, Severity = CollectionMigrationIssueSeverity.Blocker,
                        Status = CollectionMigrationIssueStatus.Open, RuleVersion = batch.RuleVersion,
                        CreatedDate = DateTimeOffset.UtcNow
                    });
                    pendingIssueCount++;
                    if (pendingIssueCount % 1000 == 0)
                    {
                        await db.SaveChangesAsync();
                        db.ChangeTracker.Clear();
                    }
                }
        if (db.ChangeTracker.HasChanges()) await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        long lastContractStageId = 0;
        while (true)
        {
            var stages = await db.Set<CollectionMigrationContractStage>()
                .Where(x => x.SourceRow.BatchId == batch.Id && x.SourceRowId > lastContractStageId)
                .OrderBy(x => x.SourceRowId).Take(500).ToListAsync();
            if (stages.Count == 0) break;
            foreach (var stage in stages)
            {
                var resolved = resolvedCustomers[stage.SourceRowId];
                stage.SubscriberNoRaw = resolved.Raw;
                stage.SubscriberNoNormalized = resolved.Normalized;
                stage.TargetCustomerId = resolved.TargetId;
                stage.TargetServiceTypeId = resolvedServices[stage.SourceRowId];
                var dates = contractDates[stage.SourceRowId];
                stage.StartingMonth = dates.Month;
                stage.StartingYear = dates.Year;
                stage.EndDate = dates.EndDate;
                stage.Status = problems.ContainsKey(stage.SourceRowId)
                    ? CollectionMigrationRowStatus.Blocked : CollectionMigrationRowStatus.Pending;
            }
            lastContractStageId = stages[^1].SourceRowId;
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
        }
        long lastRateStageId = 0;
        while (true)
        {
            var stages = await db.Set<CollectionMigrationRatePeriodStage>()
                .Where(x => x.SourceRow.BatchId == batch.Id && x.SourceRowId > lastRateStageId)
                .OrderBy(x => x.SourceRowId).Take(500).ToListAsync();
            if (stages.Count == 0) break;
            foreach (var stage in stages)
            {
                var dates = parsedHistory[stage.SourceRowId];
                stage.EffectiveFrom = dates.From;
                stage.EffectiveToExclusive = dates.ToExclusive;
                stage.TargetCurrencyTypeId = resolvedCurrencies[stage.SourceRowId];
                stage.TargetPaymentFrequencyId = resolvedFrequencies[stage.SourceRowId];
                stage.Amount = resolvedAmounts[stage.SourceRowId];
                stage.BillingBehavior = resolvedBehaviors[stage.SourceRowId];
                stage.Status = problems.ContainsKey(stage.SourceRowId)
                    ? CollectionMigrationRowStatus.Blocked : CollectionMigrationRowStatus.Pending;
            }
            lastRateStageId = stages[^1].SourceRowId;
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
        }
        batch = await db.Set<CollectionMigrationBatch>().SingleAsync(x => x.Id == batch.Id);
        batch.Status = CollectionMigrationBatchStatus.NeedsReview;
        await db.SaveChangesAsync();
        Console.WriteLine($"Doğrulama tamamlandı: {contracts.Count} sözleşme, {histories.Count} tarihçe; "
            + $"{contracts.Count(x => problems.ContainsKey(x.Id))} sözleşme ve "
            + $"{histories.Count(x => problems.ContainsKey(x.Id))} tarihçe karantinada. "
            + "Gerçek sözleşme/ödeme yüklenmedi.");

        void Add(long rowId, string code)
        {
            if (!problems.TryGetValue(rowId, out var codes)) problems[rowId] = codes = [];
            codes.Add(code);
        }
    }

    private static string? NormalizeCode(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed.ToUpper(Turkish);
    }

    private static string NormalizeId(string? value)
    {
        var trimmed = value?.Trim() ?? "";
        return long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
            ? id.ToString(CultureInfo.InvariantCulture) : trimmed;
    }

    private static string? Text(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var item) || item.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        return item.ValueKind is JsonValueKind.String ? item.GetString() : item.GetRawText();
    }

    private static bool TryMonth(JsonElement root, string yearName, string monthName, out DateOnly date)
    {
        date = default;
        if (!int.TryParse(Text(root, yearName), NumberStyles.Integer, CultureInfo.InvariantCulture, out var year)
            || !int.TryParse(Text(root, monthName), NumberStyles.Integer, CultureInfo.InvariantCulture, out var month)
            || year is < 1 or > 9998 || month is < 1 or > 12) return false;
        date = new DateOnly(year, month, 1);
        return true;
    }

    private static bool TryEndDate(JsonElement root, out DateOnly? toExclusive)
    {
        toExclusive = null;
        var text = Text(root, "EndDate");
        if (string.IsNullOrWhiteSpace(text)) return true;
        if (!DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            || date.Date >= DateTime.MaxValue.Date) return false;
        toExclusive = DateOnly.FromDateTime(date.Date.AddDays(1));
        return true;
    }

    private static decimal? ParseAmount(JsonElement root)
    {
        var raw = Text(root, "Amount");
        if (raw is null || !decimal.TryParse(raw, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var amount) || amount < 0 || amount > 9999999999999999.99m
            || decimal.Round(amount, 2) != amount)
            return null;
        return amount;
    }

    private static CollectionBillingBehavior? ParseBehavior(string? processType) => NormalizeCode(processType) switch
    {
        "BAŞLANGIÇ" or "FİYAT ARTIŞI" or "FİYAT ARTISI" or "FİYAT ARTİSİ" or "FİYAT İNDİRİMİ"
            or "TURKCELL GPRS ZAM YANSITMASI" => CollectionBillingBehavior.Billable,
        "ÜCRETSİZ" => CollectionBillingBehavior.Free,
        "HİZMET DONDURMA" => CollectionBillingBehavior.Suspended,
        _ => null
    };

    private sealed record SourceRow(long Id, string EntityCode, string SourceId, string? SourceParentId, string Payload);
    private sealed record TargetCustomer(long Id, string? Code, bool IsDeleted, long? CustomerTypeId);
    private sealed record HistoryRange(long RowId, DateOnly From, DateOnly? ToExclusive);
}
