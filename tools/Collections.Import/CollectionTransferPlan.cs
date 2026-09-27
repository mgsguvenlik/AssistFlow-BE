using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Data.Concrete.EfCore.Collections;
using Data.Concrete.EfCore.Context;
using Microsoft.EntityFrameworkCore;
using Model.Concrete.Collections;

/// <summary>Önizleme ve transaction içindeki uygulama aynı tam-küme kontrollerini kullanır.</summary>
internal sealed record CollectionTransferPlan(
    List<CollectionMigrationContractStage> Contracts,
    List<CollectionMigrationRatePeriodStage> Rates,
    string Hash, int Candidates, int Frozen, int OtherBlocked,
    IReadOnlyList<string> FrozenDateReviewIds)
{
    public static async Task<CollectionTransferPlan> LoadAsync(AppDataContext db, CollectionMigrationBatch batch,
        bool itemTwoOnly, bool frozenOnly = false, bool itemFourOnly = false, bool itemFiveOnly = false)
    {
        if (itemTwoOnly && frozenOnly) throw new InvalidOperationException("Aktarım kapsamı çelişkili.");
        if (itemFourOnly && (itemTwoOnly || frozenOnly)) throw new InvalidOperationException("Madde 4 aktarım kapsamı çelişkili.");
        if (itemFiveOnly && (itemTwoOnly || frozenOnly || itemFourOnly)) throw new InvalidOperationException("Madde 5 aktarım kapsamı çelişkili.");
        var asOf = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "Europe/Istanbul").DateTime);
        var contracts = await db.Set<CollectionMigrationContractStage>().Include(x => x.SourceRow)
            .Where(x => x.SourceRow.BatchId == batch.Id && x.Status == CollectionMigrationRowStatus.Pending)
            .OrderBy(x => x.SourceRowId).ToListAsync();
        if (itemTwoOnly)
        {
            var approved = await db.Set<CollectionMigrationIssue>().AsNoTracking()
                .Where(x => x.SourceRow.BatchId == batch.Id && x.Status == CollectionMigrationIssueStatus.Resolved
                    && x.IssueCode == "CUSTOMER_TYPE_MISMATCH" && x.ResolutionNote != null
                    && x.ResolutionNote.StartsWith("26.09.2026 madde 2:"))
                .Select(x => x.SourceRowId).ToHashSetAsync();
            contracts = contracts.Where(x => approved.Contains(x.SourceRowId)).ToList();
        }
        if (itemFourOnly)
        {
            var approved = await db.Set<CollectionMigrationIssue>().AsNoTracking()
                .Where(x => x.SourceRow.BatchId == batch.Id && x.Status == CollectionMigrationIssueStatus.Resolved
                    && x.IssueCode == "SUBSCRIBER_SOURCE_DUPLICATE" && x.ResolutionNote != null
                    && x.ResolutionNote.StartsWith("26.09.2026 madde 4:"))
                .Select(x => x.SourceRowId).ToHashSetAsync();
            contracts = contracts.Where(x => approved.Contains(x.SourceRowId)).ToList();
        }
        if (itemFiveOnly)
        {
            var approved = await db.Set<CollectionMigrationIssue>().AsNoTracking()
                .Where(x => x.SourceRow.BatchId == batch.Id && x.IssueCode == "PROCESS_TYPE_CUSTOMER_APPROVED"
                    && x.Status == CollectionMigrationIssueStatus.Resolved && x.ResolutionNote != null
                    && x.ResolutionNote.StartsWith(CollectionItemFiveDecision.ApprovalPrefix)
                    && x.ResolutionNote.Contains(CollectionItemFiveDecision.WorkbookHash))
                .Select(x => x.SourceRow.SourceParentId).ToListAsync();
            var approvedIds = approved.Select(NormalizeId).ToHashSet();
            contracts = contracts.Where(x => CollectionItemFiveDecision.ContractIds.Contains(x.SourceRow.SourceId)
                && approvedIds.Contains(x.SourceRow.SourceId)).ToList();
        }
        // Tüm durumlar okunur: yalnız Pending tarihçeyi almak eksik bir zaman çizgisi aktarabilir.
        var rates = await db.Set<CollectionMigrationRatePeriodStage>().Include(x => x.SourceRow)
            .Where(x => x.SourceRow.BatchId == batch.Id).OrderBy(x => x.SourceRowId).ToListAsync();
        var periodsByParent = rates.ToLookup(x => NormalizeId(x.SourceContractId));
        var blockers = await db.Set<CollectionMigrationIssue>().AsNoTracking()
            .Where(x => x.SourceRow.BatchId == batch.Id && x.Status == CollectionMigrationIssueStatus.Open
                && x.Severity == CollectionMigrationIssueSeverity.Blocker).Select(x => x.SourceRowId).ToHashSetAsync();
        var refs = await db.Set<CollectionMigrationReferenceMap>().AsNoTracking()
            .Where(x => x.BatchId == batch.Id && x.Status == CollectionMigrationDecisionStatus.Accepted)
            .OrderBy(x => x.Id).ToListAsync();
        var referenceMap = refs.ToDictionary(x => (x.ReferenceKind, NormalizeId(x.SourceId)));
        var subscriptions = await db.Set<CollectionSubscriptionStatus>().AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Code);
        string? SubscriptionCode(CollectionMigrationContractStage stage)
        {
            using var payload = JsonDocument.Parse(stage.SourceRow.Payload);
            var sourceId = NormalizeId(Text(payload.RootElement, "SubscriptionStatusID"));
            var targetId = referenceMap.GetValueOrDefault(("SubscriptionStatus", sourceId))?.TargetSubscriptionStatusId;
            return targetId.HasValue ? subscriptions.GetValueOrDefault(targetId.Value) : null;
        }
        if (frozenOnly) contracts = contracts.Where(x => SubscriptionCode(x) == "FROZEN").ToList();
        if (itemTwoOnly) contracts = contracts.Where(x => SubscriptionCode(x) == "ACTIVE").ToList();
        var statuses = await db.Set<CollectionContractStatus>().AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Code);
        var customerIds = await CollectionCustomerScopeQuery.Customers(db.Customers).Select(x => x.Id).ToHashSetAsync();
        var customers = await db.Customers.AsNoTracking().Select(x => new
        {
            x.Id, x.SubscriberCode, x.CustomerTypeId, x.CustomerGroupId,
            GroupCode = x.CustomerGroup == null ? null : x.CustomerGroup.Code,
            TypeCode = x.CustomerType == null ? null : x.CustomerType.Code
        }).ToListAsync();
        var byCustomerId = customers.ToDictionary(x => x.Id);
        var duplicateSubscribers = customers.GroupBy(x => CollectionCustomerClassification.Normalize(x.SubscriberCode))
            .Where(x => x.Count() > 1).Select(x => x.Key).ToHashSet();
        var services = await db.ServiceTypes.AsNoTracking().Where(x => !x.IsDeleted).Select(x => x.Id).ToHashSetAsync();
        var currencies = await db.CurrencyTypes.AsNoTracking().Select(x => x.Id).ToHashSetAsync();
        var frequencies = await db.Set<CollectionPaymentFrequency>().AsNoTracking()
            .ToDictionaryAsync(x => x.Id, x => x.IntervalMonths);
        var maps = await db.Set<CollectionMigrationMap>().AsNoTracking().Where(x => x.SourceSystem == batch.SourceSystem)
            .Select(x => new { x.EntityCode, x.SourceId }).ToListAsync();
        var mapped = maps.Select(x => (x.EntityCode, NormalizeId(x.SourceId))).ToHashSet();
        var identityDecision = await CollectionCustomerDuplicateReviewer.EligibleCustomersAsync(db, batch.Id);
        var selected = new List<CollectionMigrationContractStage>();
        var selectedRates = new List<CollectionMigrationRatePeriodStage>();
        var frozen = 0;
        var frozenDateReviewIds = new List<string>();
        foreach (var contract in contracts)
        {
            if (!identityDecision.CustomerIds.Contains(NormalizeId(contract.SourceCustomerId))) continue;
            using var payload = JsonDocument.Parse(contract.SourceRow.Payload);
            var root = payload.RootElement;
            var subscriptionSource = NormalizeId(Text(root, "SubscriptionStatusID"));
            var subscriptionId = referenceMap.GetValueOrDefault(("SubscriptionStatus", subscriptionSource))?.TargetSubscriptionStatusId;
            var subscriptionCode = subscriptionId.HasValue ? subscriptions.GetValueOrDefault(subscriptionId.Value) : null;
            if (subscriptionCode == "FROZEN") frozen++;
            if (subscriptionCode is not ("ACTIVE" or "FROZEN")) continue;
            var statusSource = NormalizeId(Text(root, "ContractStatusID"));
            var statusId = referenceMap.GetValueOrDefault(("ContractStatus", statusSource))?.TargetContractStatusId;
            if (statusSource.Length != 0 && (!statusId.HasValue || statuses.GetValueOrDefault(statusId.Value) is not ("EXISTS" or "UNKNOWN"))) continue;
            if (mapped.Contains(("Contract", NormalizeId(contract.SourceRow.SourceId))) || blockers.Contains(contract.SourceRowId)
                || contract.TargetCustomerId is not { } customerId || !customerIds.Contains(customerId)
                || contract.TargetServiceTypeId is not { } serviceId || !services.Contains(serviceId)
                || contract.StartingMonth is not (>= 1 and <= 12) || contract.StartingYear is not (>= 1 and < 9999)) continue;
            var subscriber = CollectionCustomerClassification.Normalize(byCustomerId[customerId].SubscriberCode);
            if (subscriber.Length == 0 || subscriber != contract.SubscriberNoNormalized || duplicateSubscribers.Contains(subscriber)) continue;
            var periods = periodsByParent[NormalizeId(contract.SourceRow.SourceId)].OrderBy(x => x.EffectiveFrom).ToList();
            if (periods.Count == 0 || periods.Any(x => x.Status != CollectionMigrationRowStatus.Pending
                    || blockers.Contains(x.SourceRowId) || mapped.Contains(("ContractHistory", NormalizeId(x.SourceRow.SourceId)))
                    || x.EffectiveFrom is null || x.EffectiveToExclusive <= x.EffectiveFrom
                    || x.Amount is null or < 0 || x.TargetCurrencyTypeId is not { } currency || !currencies.Contains(currency)
                    || x.TargetPaymentFrequencyId is not { } frequency || !frequencies.ContainsKey(frequency)
                    || frequencies[frequency] is not (1 or 2 or 3 or 4 or 6 or 12 or 24 or 36) || x.BillingBehavior is null)) continue;
            if (periods.Zip(periods.Skip(1)).Any(x => x.First.EffectiveToExclusive is null
                    || x.First.EffectiveToExclusive > x.Second.EffectiveFrom)) continue;
            // Güncel Donuk etiketi geçmişi yeniden yorumlamaz. Tarihi bilinmeyen ücretli
            // açık dönem tahminle kapatılmaz; borç üretme riski olan kayıt incelemede kalır.
            if (subscriptionCode == "FROZEN" && (contract.EndDate is null || contract.EndDate >= asOf)
                && periods.Any(x => x.BillingBehavior == CollectionBillingBehavior.Billable
                    && (x.EffectiveToExclusive is null || x.EffectiveToExclusive > asOf)
                    && (contract.EndDate is null || x.EffectiveFrom <= contract.EndDate)))
            {
                frozenDateReviewIds.Add(NormalizeId(contract.SourceRow.SourceId));
                continue;
            }
            foreach (var source in periods.Select(x => x.SourceRow).Append(contract.SourceRow))
                if (!SHA256.HashData(Encoding.UTF8.GetBytes(source.Payload)).SequenceEqual(source.PayloadHash))
                    throw new InvalidOperationException("Staging ham veri hash'i değişmiş; aktarım durduruldu.");
            selected.Add(contract);
            selectedRates.AddRange(periods);
        }
        // Hash yalnız kaynak kimliği değil, kullanılacak hedefleri, tutarları ve tüm tarihçeyi de bağlar.
        var material = JsonSerializer.Serialize(new
        {
            Version = "collection-transfer-v6-item-five", itemTwoOnly, frozenOnly, itemFourOnly, itemFiveOnly, asOf, batch.Id, batch.ManifestHash,
            IdentityPolicyHash = identityDecision.Hash,
            CollectionCustomerClassification.PolicyHash,
            Contracts = selected.Select(x => new { x.SourceRowId, x.SourceRow.SourceId, x.SourceRow.PayloadHash, x.RowVersion,
                x.TargetCustomerId, x.TargetServiceTypeId, x.StartingMonth, x.StartingYear, x.EndDate }),
            Rates = selectedRates.Select(x => new { x.SourceRowId, x.SourceRow.PayloadHash, x.RowVersion,
                x.EffectiveFrom, x.EffectiveToExclusive, x.Amount, x.TargetCurrencyTypeId, x.TargetPaymentFrequencyId, x.BillingBehavior }),
            Customers = selected.Select(x => byCustomerId[x.TargetCustomerId!.Value]).Distinct().OrderBy(x => x.Id),
            References = refs.Select(x => new { x.Id, x.RowVersion })
        });
        return new(selected, selectedRates, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material))),
            contracts.Count, frozen, contracts.Count - selected.Count, frozenDateReviewIds);
    }

    public void Print()
    {
        Console.WriteLine($"Aday={Candidates}; aktarılabilir={Contracts.Count}; donuk aday={Frozen}; bekleyen={OtherBlocked}; tarife={Rates.Count}.");
        if (FrozenDateReviewIds.Count > 0)
            Console.WriteLine($"Donma/bitiş tarihi incelemesi gereken kaynak sözleşmeler: {string.Join(", ", FrozenDateReviewIds)}");
        Console.WriteLine($"Plan SHA-256: {Hash}");
        foreach (var group in Rates.GroupBy(x => new { x.TargetCurrencyTypeId, x.BillingBehavior })
                     .OrderBy(x => x.Key.TargetCurrencyTypeId).ThenBy(x => x.Key.BillingBehavior))
            Console.WriteLine($"  {group.Key.TargetCurrencyTypeId}|{group.Key.BillingBehavior}|{group.Count()}|{group.Sum(x => x.Amount):0.00}");
    }

    public static string NormalizeId(string? value) => long.TryParse(value?.Trim(), out var id)
        ? id.ToString(CultureInfo.InvariantCulture) : value?.Trim() ?? "";
    private static string? Text(JsonElement root, string name) => root.TryGetProperty(name, out var value)
        && value.ValueKind != JsonValueKind.Null ? value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText() : null;
}
