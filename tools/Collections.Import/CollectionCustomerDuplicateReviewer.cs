using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Data.Concrete.EfCore.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Model.Concrete.Collections;

/// <summary>Madde 4: aynı abonenin tekil en yeni müşterisini seçer; kaynak veya dbo müşterisi silmez.</summary>
internal static class CollectionCustomerDuplicateReviewer
{
    private const string Note = "26.09.2026 madde 4: aynı abone numarasında CreatedOn tarihi tekil en yeni müşteri korunur.";
    private static readonly string[] ResolvableCodes = ["SUBSCRIBER_SOURCE_DUPLICATE", "CUSTOMER_TYPE_MAP_MISSING",
        "CUSTOMER_TYPE_MISMATCH", "CUSTOMER_COLLECTION_UNKNOWN", "CUSTOMER_COLLECTION_EXCLUDED"];

    public static async Task RunAsync(long batchId, string settingsPath, string? expectedPlanHash, bool byName = false)
    {
        var decisionNote = byName
            ? "26.09.2026 madde 4: aynı isimde farklı aboneler dahil yalnız CreatedOn tarihi tekil en yeni abonelik ve sözleşmeleri korunur."
            : Note;
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath), new JsonDocumentOptions
        { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        var connection = new SqlConnectionStringBuilder(settings.RootElement.GetProperty("AppSettings").GetProperty("MSSQLConnectionString").GetString());
        if (connection.DataSource != "192.168.1.8" || connection.InitialCatalog != "AssistFlowTest" || batchId <= 0)
            throw new InvalidOperationException("Madde 4 incelemesi yalnız geçerli AssistFlowTest kesitinde yapılır.");
        await using var db = new AppDataContext(new DbContextOptionsBuilder<AppDataContext>()
            .UseSqlServer(connection.ConnectionString, sql => sql.CommandTimeout(180)).Options);
        await using var tx = expectedPlanHash is null ? null
            : await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        if (tx is not null)
            await db.Database.ExecuteSqlRawAsync("""
                DECLARE @result int;
                EXEC @result = sys.sp_getapplock @Resource=N'CollectionLegacyContractApply',
                    @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=15000;
                IF @result<0 THROW 51000, N'Aktarım karar kilidi alınamadı.', 1;
                """);
        var batch = await db.Set<CollectionMigrationBatch>().AsNoTracking().SingleAsync(x => x.Id == batchId);
        if (batch.Status != CollectionMigrationBatchStatus.NeedsReview)
            throw new InvalidOperationException("Kesit inceleme durumunda değil.");
        var sourceRows = await db.Set<CollectionMigrationSourceRow>().AsNoTracking()
            .Where(x => x.BatchId == batchId && x.EntityCode == "Customer").OrderBy(x => x.Id).ToListAsync();
        var customers = sourceRows.Select(ParseCustomer).ToList();
        var contracts = await db.Set<CollectionMigrationContractStage>().Include(x => x.SourceRow)
            .Where(x => x.SourceRow.BatchId == batchId).OrderBy(x => x.SourceRowId).ToListAsync();
        var contractsByCustomer = contracts.ToLookup(x => Id(x.SourceCustomerId));
        var rates = await db.Set<CollectionMigrationRatePeriodStage>().Include(x => x.SourceRow)
            .Where(x => x.SourceRow.BatchId == batchId).OrderBy(x => x.SourceRowId).ToListAsync();
        var ratesByContract = rates.ToLookup(x => Id(x.SourceContractId));
        var issues = await db.Set<CollectionMigrationIssue>().Where(x => x.SourceRow.BatchId == batchId
            && x.Status == CollectionMigrationIssueStatus.Open).OrderBy(x => x.Id).ToListAsync();
        var byIssueRow = issues.ToLookup(x => x.SourceRowId);
        var maps = await db.Set<CollectionMigrationMap>().AsNoTracking()
            .Where(x => x.SourceSystem == batch.SourceSystem).OrderBy(x => x.Id).ToListAsync();
        var mapped = maps.Select(x => (x.EntityCode, Id(x.SourceId))).ToHashSet();
        var targets = await db.Customers.AsNoTracking().OrderBy(x => x.Id).Select(x => new
        {
            x.Id, x.SubscriberCode, x.IsDeleted, x.CustomerTypeId, x.CustomerGroupId,
            GroupCode = x.CustomerGroup == null ? null : x.CustomerGroup.Code,
            TypeCode = x.CustomerType == null ? null : x.CustomerType.Code
        }).ToListAsync();
        var targetsBySubscriber = targets.ToLookup(x => CollectionCustomerClassification.Normalize(x.SubscriberCode));
        var customersBySubscriber = customers.Where(x => x.Subscriber.Length > 0).ToLookup(x => x.Subscriber);
        var groups = customers.Where(x => (byName ? x.Name : x.Subscriber).Length > 0).GroupBy(x => byName ? x.Name : x.Subscriber)
            .Where(x => x.Count() > 1).OrderBy(x => x.Key, StringComparer.Ordinal).ToList();
        var actions = new List<Decision>();
        var report = new List<object>();
        foreach (var group in groups)
        {
            var ordered = group.OrderByDescending(x => x.CreatedOn).ThenBy(x => x.Id, StringComparer.Ordinal).ToList();
            var winner = ordered[0];
            string? held = ordered.Any(x => x.CreatedOn is null) ? "Kayıt tarihi eksik/geçersiz"
                : ordered[1].CreatedOn == winner.CreatedOn ? "En yeni kayıt tarihi eşit" : null;
            if (held is null && byName)
            {
                var sameSubscriber = customersBySubscriber[winner.Subscriber].OrderByDescending(x => x.CreatedOn).ToList();
                if (winner.Subscriber.Length == 0) held = "En yeni isim kaydının abone numarası boş";
                else if (sameSubscriber.Any(x => x.CreatedOn is null)
                    || sameSubscriber.Count(x => x.CreatedOn == sameSubscriber[0].CreatedOn) != 1
                    || sameSubscriber[0].Id != winner.Id)
                    held = "İsim ve abone numarası kurallarının en yeni müşteri seçimi çelişiyor";
            }
            var older = ordered.Skip(1).ToList();
            if (held is null && older.SelectMany(x => contractsByCustomer[x.Id]).Any(c =>
                    IsApplied(c.Status) || mapped.Contains(("Contract", Id(c.SourceRow.SourceId)))
                    || ratesByContract[Id(c.SourceRow.SourceId)].Any(r => IsApplied(r.Status)
                        || mapped.Contains(("ContractHistory", Id(r.SourceRow.SourceId))))))
                held = "Eski müşteriyle daha önce aktarılmış kayıt var; otomatik silinmez";
            report.Add(new { GroupKey = group.Key, Winner = held is null ? winner.Id : null, Held = held,
                Customers = ordered.Select(x => new { x.Id, x.Name, x.CreatedOn,
                    Contracts = contractsByCustomer[x.Id].Select(c => new { c.SourceRow.SourceId, c.Status }) }) });
            if (held is not null) continue;
            foreach (var old in older)
            foreach (var contract in contractsByCustomer[old.Id].Where(x => CanChange(x.Status)))
            {
                var histories = ratesByContract[Id(contract.SourceRow.SourceId)].Where(x => CanChange(x.Status)).ToList();
                var rowIds = histories.Select(x => x.SourceRowId).Append(contract.SourceRowId).ToHashSet();
                actions.Add(new(contract.SourceRowId, contract.SourceRow.SourceId, old.Id, winner.Id,
                    true, null, CollectionMigrationRowStatus.Excluded,
                    issues.Where(x => rowIds.Contains(x.SourceRowId)).Select(x => x.Id).ToArray(),
                    histories.Select(x => x.SourceRowId).ToArray()));
            }
            // Yeni müşterinin sözleşmeleri birbiriyle birleştirilmez: farklı hizmetler olabilir.
            var matchingTargets = targetsBySubscriber[winner.Subscriber].ToList();
            if (matchingTargets.Count != 1 || matchingTargets[0].IsDeleted
                || CollectionCustomerClassification.Issue(matchingTargets[0].GroupCode, matchingTargets[0].TypeCode) is not null)
                continue; // Madde 3 / hedef belirsizliği ve kapsam kararları korunur.
            var target = matchingTargets[0];
            foreach (var contract in contractsByCustomer[winner.Id].Where(x => CanChange(x.Status)))
            {
                var open = byIssueRow[contract.SourceRowId].ToList();
                if (!open.Any(x => x.IssueCode == "SUBSCRIBER_SOURCE_DUPLICATE")
                    || mapped.Contains(("Contract", Id(contract.SourceRow.SourceId)))
                    || contract.SubscriberNoNormalized != winner.Subscriber
                    || contract.TargetCustomerId is { } existing && existing != target.Id) continue;
                var resolve = open.Where(x => ResolvableCodes.Contains(x.IssueCode)).Select(x => x.Id).ToArray();
                var histories = ratesByContract[Id(contract.SourceRow.SourceId)].ToList();
                var blocked = open.Any(x => !resolve.Contains(x.Id) && x.Severity == CollectionMigrationIssueSeverity.Blocker)
                    || histories.Count == 0 || histories.Any(x => x.Status != CollectionMigrationRowStatus.Pending
                        || byIssueRow[x.SourceRowId].Any(i => i.Severity == CollectionMigrationIssueSeverity.Blocker))
                    || contract.TargetServiceTypeId is null || contract.StartingMonth is null || contract.StartingYear is null;
                actions.Add(new(contract.SourceRowId, contract.SourceRow.SourceId, winner.Id, winner.Id,
                    false, target.Id, blocked ? CollectionMigrationRowStatus.Blocked : CollectionMigrationRowStatus.Pending,
                    resolve, []));
            }
        }
        var sameNames = customers.Where(x => x.Name.Length > 0).GroupBy(x => x.Name)
            .Where(x => x.Count() > 1).OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => new
            {
                Name = x.Key, DistinctSubscribers = x.Where(c => c.Subscriber.Length > 0).Select(c => c.Subscriber).Distinct().Count(),
                DistinctAddresses = x.Where(c => c.Address.Length > 0).Select(c => c.Address).Distinct().Count(),
                DistinctTaxNumbers = x.Where(c => c.TaxNo.Length > 0).Select(c => c.TaxNo).Distinct().Count(),
                DistinctGroups = x.Select(c => c.GroupKey).Distinct().Count(),
                Customers = x.Select(c => new { c.Id, c.Subscriber, c.CreatedOn, c.Address, c.TaxNo, c.GroupKey,
                    Contracts = contractsByCustomer[c.Id].Select(s => new { s.SourceRow.SourceId, s.Status, s.TargetCustomerId }) }).ToArray(),
                ContractCount = x.Sum(c => contractsByCustomer[c.Id].Count()),
                AppliedContractCount = x.Sum(c => contractsByCustomer[c.Id].Count(s => IsApplied(s.Status)))
            }).ToArray();
        var emptyIds = customers.Where(x => x.Name.Length == 0 && x.Subscriber.Length == 0).Select(x => x.Id).ToHashSet();
        var emptyContracts = contracts.Where(x => emptyIds.Contains(Id(x.SourceCustomerId))).ToList();
        var material = JsonSerializer.Serialize(new
        {
            Version = "item-four-created-on-v2", byName, batch.Id, batch.ManifestHash, CollectionCustomerClassification.PolicyHash,
            SourceCustomers = sourceRows.Select(x => new { x.Id, x.PayloadHash }), Targets = targets,
            Contracts = contracts.Select(x => new { x.SourceRowId, x.SourceRow.PayloadHash, x.RowVersion, x.Status, x.TargetCustomerId }),
            Rates = rates.Select(x => new { x.SourceRowId, x.SourceRow.PayloadHash, x.RowVersion, x.Status }),
            Issues = issues.Select(x => new { x.Id, x.SourceRowId, x.IssueCode, x.Severity, x.Status }),
            Maps = maps.Select(x => new { x.Id, x.RowVersion }), Actions = actions
        });
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
        var output = Path.Combine("tools", "Collections.Import", "snapshots", (byName ? "item-four-names-" : "item-four-") + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff"));
        Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(Path.Combine(output, "plan.json"), JsonSerializer.Serialize(new
        {
            Hash = hash, ByName = byName, Actions = actions, DuplicateGroups = report, SameNameGroups = sameNames,
            EmptyContracts = emptyContracts.Select(x => new { x.SourceRow.SourceId, x.Status })
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"{(byName ? "Aynı isim" : "Aynı abone")} kümesi={groups.Count}; eski sözleşme dışlama={actions.Count(x => x.Exclude)}; bağlı tarife dışlama={actions.Sum(x => x.RateRowIds.Length)}.");
        Console.WriteLine($"En yeni müşteriyle eşleşen sözleşme={actions.Count(x => !x.Exclude)}; Pending olacak={actions.Count(x => !x.Exclude && x.Status == CollectionMigrationRowStatus.Pending)}.");
        Console.WriteLine($"Aynı isim/farklı abone kümeleri={sameNames.Count(x => x.DistinctSubscribers > 1)}; abonesiz aynı isim kümeleri={sameNames.Count(x => x.DistinctSubscribers == 0)}. İşlem kapsamı={(byName ? "isim bazlı en yeni abonelik" : "abone numarası bazlı en yeni müşteri")}.");
        Console.WriteLine($"Farklı aboneli isim kümelerinde: adres çelişkisi={sameNames.Count(x => x.DistinctSubscribers > 1 && x.DistinctAddresses > 1)}; grup çelişkisi={sameNames.Count(x => x.DistinctSubscribers > 1 && x.DistinctGroups > 1)}; önceden aktarılmış sözleşme içeren küme={sameNames.Count(x => x.DistinctSubscribers > 1 && x.AppliedContractCount > 0)}.");
        Console.WriteLine($"Adı ve abonesi boş sözleşmeler: toplam={emptyContracts.Count}, önceden dışlanan={emptyContracts.Count(x => x.Status == CollectionMigrationRowStatus.Excluded)}, yeni inceleme={emptyContracts.Count(x => CanChange(x.Status))}.");
        Console.WriteLine($"Plan SHA-256: {hash}\nYerel rapor: {Path.GetFullPath(output)}");
        if (expectedPlanHash is null) { Console.WriteLine("DRY-RUN; veri değiştirilmedi."); return; }
        if (!hash.Equals(expectedPlanHash, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Madde 4 planı değişmiş; tekrar önizleme gereklidir.");
        var byContractRow = contracts.ToDictionary(x => x.SourceRowId);
        var byRateRow = rates.ToDictionary(x => x.SourceRowId);
        var byIssueId = issues.ToDictionary(x => x.Id);
        var now = DateTimeOffset.UtcNow;
        foreach (var action in actions)
        {
            var contract = byContractRow[action.RowId];
            Verify(contract.SourceRow);
            var note = $"{decisionNote} Eski/kaynak müşteri={action.CustomerId}; korunan müşteri={action.WinnerId}; {(action.Exclude ? "eski kayıt aktarım dışında" : "tekil hedef eşleşmesi doğrulandı")}.";
            contract.Status = action.Status;
            if (!action.Exclude) contract.TargetCustomerId = action.TargetId;
            foreach (var rateId in action.RateRowIds)
            {
                var rate = byRateRow[rateId];
                Verify(rate.SourceRow);
                rate.Status = CollectionMigrationRowStatus.Excluded;
            }
            foreach (var issueId in action.IssueIds)
            {
                var issue = byIssueId[issueId];
                issue.Status = action.Exclude ? CollectionMigrationIssueStatus.Ignored : CollectionMigrationIssueStatus.Resolved;
                issue.ResolutionNote = note;
                issue.ResolvedDate = now;
                issue.ResolvedUser = null;
            }
            if (action.Exclude)
                db.Set<CollectionMigrationIssue>().Add(new()
                {
                    SourceRowId = action.RowId, IssueCode = byName ? "CUSTOMER_NAME_SUPERSEDED" : "CUSTOMER_DUPLICATE_SUPERSEDED", Severity = CollectionMigrationIssueSeverity.Warning,
                    Status = CollectionMigrationIssueStatus.Resolved, Details = note, ResolutionNote = note,
                    RuleVersion = batch.RuleVersion, CreatedDate = now, ResolvedDate = now
                });
        }
        await db.SaveChangesAsync();
        await tx!.CommitAsync();
        Console.WriteLine($"Madde 4 staging kararı uygulandı: {actions.Count} sözleşme. Kaynak/hedef müşteri silinmedi; finansal aktarım yapılmadı.");
    }

    private sealed record SourceCustomer(string Id, string Subscriber, string Name, DateTime? CreatedOn,
        string Address, string TaxNo, string GroupKey);

    internal static Dictionary<string, string> LatestNameReplacements(IEnumerable<CollectionMigrationSourceRow> sources)
    {
        var customers = sources.Select(ParseCustomer).ToList();
        var bySubscriber = customers.Where(x => x.Subscriber.Length > 0).ToLookup(x => x.Subscriber);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var group in customers.Where(x => x.Name.Length > 0).GroupBy(x => x.Name).Where(x => x.Count() > 1))
        {
            var ordered = group.OrderByDescending(x => x.CreatedOn).ToList();
            var winner = ordered[0];
            if (ordered.Any(x => x.CreatedOn is null) || ordered[1].CreatedOn == winner.CreatedOn || winner.Subscriber.Length == 0) continue;
            var sameSubscriber = bySubscriber[winner.Subscriber].OrderByDescending(x => x.CreatedOn).ToList();
            if (sameSubscriber.Any(x => x.CreatedOn is null) || sameSubscriber.Count(x => x.CreatedOn == sameSubscriber[0].CreatedOn) != 1
                || sameSubscriber[0].Id != winner.Id) continue;
            foreach (var older in ordered.Skip(1)) result.Add(older.Id, winner.Id);
        }
        return result;
    }

    // Aktarım komutu da kararı bağımsız denetler; eski bir Pending satır karar incelemesini atlayamaz.
    public static async Task<(HashSet<string> CustomerIds, string Hash)> EligibleCustomersAsync(AppDataContext db, long batchId)
    {
        var sources = await db.Set<CollectionMigrationSourceRow>().AsNoTracking()
            .Where(x => x.BatchId == batchId && x.EntityCode == "Customer").OrderBy(x => x.Id).ToListAsync();
        var customers = sources.Select(ParseCustomer).ToList();
        var appliedIds = (await db.Set<CollectionMigrationContractStage>().AsNoTracking()
            .Where(x => x.SourceRow.BatchId == batchId && (x.Status == CollectionMigrationRowStatus.Applied
                || x.Status == CollectionMigrationRowStatus.AlreadyApplied))
            .Select(x => x.SourceCustomerId).ToListAsync()).Select(Id).ToHashSet();
        var eligible = customers.Where(x => x.Subscriber.Length > 0).Select(x => x.Id).ToHashSet();
        foreach (var groups in new[]
        {
            customers.Where(x => x.Subscriber.Length > 0).GroupBy(x => x.Subscriber),
            customers.Where(x => x.Name.Length > 0).GroupBy(x => x.Name)
        })
        foreach (var group in groups.Where(x => x.Count() > 1))
        {
            var ordered = group.OrderByDescending(x => x.CreatedOn).ToList();
            var valid = ordered.All(x => x.CreatedOn.HasValue) && ordered[0].CreatedOn != ordered[1].CreatedOn
                && !ordered.Skip(1).Any(x => appliedIds.Contains(x.Id));
            foreach (var customer in valid ? ordered.Skip(1) : ordered) eligible.Remove(customer.Id);
        }
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            Policy = "item-four-latest-subscriber-and-name-v1", batchId,
            Sources = sources.Select(x => new { x.Id, x.PayloadHash }),
            Applied = appliedIds.OrderBy(x => x, StringComparer.Ordinal)
        }))));
        return (eligible, hash);
    }
    private sealed record Decision(long RowId, string ContractId, string CustomerId, string WinnerId,
        bool Exclude, long? TargetId, CollectionMigrationRowStatus Status, long[] IssueIds, long[] RateRowIds);
    private static bool IsApplied(CollectionMigrationRowStatus status) => status is CollectionMigrationRowStatus.Applied or CollectionMigrationRowStatus.AlreadyApplied;
    private static bool CanChange(CollectionMigrationRowStatus status) => status is CollectionMigrationRowStatus.Pending or CollectionMigrationRowStatus.Blocked;
    private static string Id(string? value) => CollectionTransferPlan.NormalizeId(value);
    private static SourceCustomer ParseCustomer(CollectionMigrationSourceRow row)
    {
        Verify(row);
        using var payload = JsonDocument.Parse(row.Payload);
        var root = payload.RootElement;
        string? Text(string name) => root.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null ? value.ToString() : null;
        string NormalizeName(string? value) => Regex.Replace((value ?? "").Trim(), @"\s+", " ").ToUpper(CultureInfo.GetCultureInfo("tr-TR"));
        var name = NormalizeName(Text("Name"));
        DateTime? created = DateTime.TryParse(Text("CreatedOn"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date) ? date : null;
        return new(Id(row.SourceId), CollectionCustomerClassification.Normalize(Text("SubscriberNo")), name, created,
            NormalizeName(Text("Adress")), (Text("TaxNo") ?? "").Trim(), $"{Text("Type")}/{Id(Text("GroupID"))}");
    }
    private static void Verify(CollectionMigrationSourceRow source)
    {
        if (!SHA256.HashData(Encoding.UTF8.GetBytes(source.Payload)).SequenceEqual(source.PayloadHash))
            throw new InvalidOperationException("Kaynak kesit hash'i değişmiş; karar uygulanmadı.");
    }
}
