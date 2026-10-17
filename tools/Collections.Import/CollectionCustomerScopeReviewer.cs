using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Data.Concrete.EfCore.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Model.Concrete.Collections;

/// <summary>Madde 2: kısmen aktarılmış kesitte yalnız müşteri tipi/kapsam issue'larını yeniler.</summary>
internal static class CollectionCustomerScopeReviewer
{
    private static readonly string[] ScopeCodes = ["CUSTOMER_TYPE_MISMATCH", "CUSTOMER_TYPE_MAP_MISSING",
        "CUSTOMER_COLLECTION_EXCLUDED", "CUSTOMER_COLLECTION_UNKNOWN"];
    private const string Note = "26.09.2026 madde 2: onaylı CustomerGroups.Code sınıflandırması; STB/STBG bireysel. Diğer veri kararları değiştirilmedi.";

    public static async Task RunStoredBatchAsync(long batchId, string settingsPath, string? expectedPlanHash)
    {
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath), new JsonDocumentOptions
        { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        var connection = new SqlConnectionStringBuilder(settings.RootElement.GetProperty("AppSettings")
            .GetProperty("MSSQLConnectionString").GetString());
        if (connection.DataSource != "192.168.1.8" || connection.InitialCatalog != "AssistFlowTest" || batchId <= 0)
            throw new InvalidOperationException("Geçerli test staging kesiti gereklidir.");
        await using var db = new AppDataContext(new DbContextOptionsBuilder<AppDataContext>()
            .UseSqlServer(connection.ConnectionString).Options);
        var batch = await db.Set<CollectionMigrationBatch>().AsNoTracking().SingleAsync(x => x.Id == batchId);
        // Bu komut kaynak dosya yüklemez; zaten staging'de bulunan değişmez kesiti ve hash'ini kullanır.
        await RunAsync(batch.SourceSystem, batch.SnapshotKey, batch.ManifestHash, settingsPath, expectedPlanHash);
    }

    public static async Task RunAsync(string sourceSystem, string snapshotKey, byte[] manifestHash,
        string settingsPath, string? expectedPlanHash)
    {
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath), new JsonDocumentOptions
        { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        var connection = new SqlConnectionStringBuilder(settings.RootElement.GetProperty("AppSettings")
            .GetProperty("MSSQLConnectionString").GetString());
        if (connection.DataSource != "192.168.1.8" || connection.InitialCatalog != "AssistFlowTest")
            throw new InvalidOperationException("Müşteri kapsam kararı yalnız AssistFlowTest üzerinde uygulanır.");
        await using var db = new AppDataContext(new DbContextOptionsBuilder<AppDataContext>()
            .UseSqlServer(connection.ConnectionString, sql => sql.CommandTimeout(120)).Options);
        await using var tx = expectedPlanHash is null ? null
            : await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        if (tx is not null)
            await db.Database.ExecuteSqlRawAsync("""
                DECLARE @result int;
                EXEC @result = sys.sp_getapplock @Resource=N'CollectionLegacyContractApply',
                    @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=15000;
                IF @result<0 THROW 51000, N'Aktarım kapsam kilidi alınamadı.', 1;
                """);
        var batch = await db.Set<CollectionMigrationBatch>().AsNoTracking().SingleAsync(x =>
            x.SourceSystem == sourceSystem && x.SnapshotKey == snapshotKey);
        if (!batch.ManifestHash.SequenceEqual(manifestHash) || batch.Status != CollectionMigrationBatchStatus.NeedsReview)
            throw new InvalidOperationException("Kesit veya inceleme durumu değişmiş.");
        var rows = await db.Set<CollectionMigrationContractStage>().Include(x => x.SourceRow)
            .Where(x => x.SourceRow.BatchId == batch.Id && x.TargetCustomerId != null
                && (x.Status == CollectionMigrationRowStatus.Pending || x.Status == CollectionMigrationRowStatus.Blocked))
            .OrderBy(x => x.SourceRowId).ToListAsync();
        var customers = await db.Customers.AsNoTracking().OrderBy(x => x.Id).Select(x => new
        {
            x.Id, x.SubscriberCode, x.IsDeleted, x.CustomerTypeId, x.CustomerGroupId,
            TypeCode = x.CustomerType == null ? null : x.CustomerType.Code,
            GroupCode = x.CustomerGroup == null ? null : x.CustomerGroup.Code
        }).ToListAsync();
        var byId = customers.ToDictionary(x => x.Id);
        var duplicates = customers.GroupBy(x => CollectionCustomerClassification.Normalize(x.SubscriberCode))
            .Where(x => x.Count() > 1).Select(x => x.Key).ToHashSet();
        var rowIds = rows.Select(x => x.SourceRowId).ToArray();
        var issues = await db.Set<CollectionMigrationIssue>()
            .Where(x => rowIds.Contains(x.SourceRowId) && x.Status == CollectionMigrationIssueStatus.Open)
            .OrderBy(x => x.Id).ToListAsync();
        var issuesByRow = issues.ToLookup(x => x.SourceRowId);
        var histories = await db.Set<CollectionMigrationRatePeriodStage>().AsNoTracking()
            .Where(x => x.SourceRow.BatchId == batch.Id).OrderBy(x => x.SourceRowId)
            .Select(x => new { x.SourceRowId, x.SourceContractId, x.Status, x.RowVersion }).ToListAsync();
        var historiesByParent = histories.ToLookup(x => NormalizeId(x.SourceContractId));
        var actions = new List<ActionRow>();
        foreach (var row in rows)
        {
            if (!byId.TryGetValue(row.TargetCustomerId!.Value, out var customer) || customer.IsDeleted
                || string.IsNullOrEmpty(row.SubscriberNoNormalized)
                || CollectionCustomerClassification.Normalize(customer.SubscriberCode) != row.SubscriberNoNormalized
                || duplicates.Contains(row.SubscriberNoNormalized)) continue;
            var code = CollectionCustomerClassification.Issue(customer.GroupCode, customer.TypeCode);
            var open = issuesByRow[row.SourceRowId].ToList();
            if (code is null && !open.Any(x => ScopeCodes.Contains(x.IssueCode))) continue;
            var resolve = open.Where(x => ScopeCodes.Contains(x.IssueCode) && x.IssueCode != code)
                .Select(x => x.Id).ToArray();
            var addCode = code is not null && !open.Any(x => x.IssueCode == code) ? code : null;
            var periods = historiesByParent[NormalizeId(row.SourceRow.SourceId)].ToList();
            var blocked = code is not null
                || open.Any(x => !ScopeCodes.Contains(x.IssueCode) && x.Severity == CollectionMigrationIssueSeverity.Blocker)
                || periods.Count == 0 || periods.Any(x => x.Status != CollectionMigrationRowStatus.Pending)
                || row.TargetServiceTypeId is null || row.StartingMonth is null || row.StartingYear is null;
            var status = blocked ? CollectionMigrationRowStatus.Blocked : CollectionMigrationRowStatus.Pending;
            if (resolve.Length != 0 || addCode is not null || status != row.Status)
                actions.Add(new(row.SourceRowId, resolve, addCode, status));
        }
        // Hash locks source, target identity/type/group, stage versions and all outstanding blockers.
        var material = JsonSerializer.Serialize(new
        {
            CollectionCustomerClassification.PolicyHash,
            Manifest = Convert.ToHexString(manifestHash),
            Rows = rows.Select(x => new { x.SourceRowId, x.TargetCustomerId, x.Status, x.RowVersion, x.SourceRow.PayloadHash }),
            Customers = customers,
            Issues = issues.Select(x => new { x.Id, x.SourceRowId, x.IssueCode, x.Severity, x.Status }),
            Histories = histories,
            Actions = actions
        });
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
        Console.WriteLine($"Madde 2 planı: {actions.Count} sözleşme değişikliği; {actions.Sum(x => x.ResolveIds.Length)} müşteri tipi/kapsam issue kapanacak.");
        Console.WriteLine($"Aktarım adayı olacak: {actions.Count(x => x.Status == CollectionMigrationRowStatus.Pending)}; engelli kalacak/olacak: {actions.Count(x => x.Status == CollectionMigrationRowStatus.Blocked)}.");
        foreach (var group in actions.Where(x => x.AddCode is not null).GroupBy(x => x.AddCode))
            Console.WriteLine($"Yeni engel {group.Key}: {group.Count()}.");
        Console.WriteLine($"Plan SHA-256: {hash}");
        if (expectedPlanHash is null)
        {
            Console.WriteLine("DRY-RUN; kayıt değiştirilmedi. Applied/Excluded, tarifeler, eşleşmeler ve diğer madde issue'ları korunur.");
            return;
        }
        if (!hash.Equals(expectedPlanHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Madde 2 planı değişti. Yeniden önizleme gereklidir.");
        var issueById = issues.ToDictionary(x => x.Id);
        var stageById = rows.ToDictionary(x => x.SourceRowId);
        var now = DateTimeOffset.UtcNow;
        foreach (var action in actions)
        {
            foreach (var id in action.ResolveIds)
            {
                var issue = issueById[id];
                issue.Status = CollectionMigrationIssueStatus.Resolved;
                issue.ResolvedDate = now;
                issue.ResolutionNote = Note;
            }
            if (action.AddCode is not null)
                db.Set<CollectionMigrationIssue>().Add(new()
                {
                    SourceRowId = action.RowId, IssueCode = action.AddCode,
                    Severity = CollectionMigrationIssueSeverity.Blocker, Status = CollectionMigrationIssueStatus.Open,
                    RuleVersion = batch.RuleVersion, Details = Note, CreatedDate = now
                });
            stageById[action.RowId].Status = action.Status;
        }
        await db.SaveChangesAsync();
        await tx!.CommitAsync();
        Console.WriteLine("Madde 2 staging kararı uygulandı. Hedef sözleşme/ödeme oluşturulmadı veya silinmedi; diğer maddeler değiştirilmedi.");
    }

    private sealed record ActionRow(long RowId, long[] ResolveIds, string? AddCode, CollectionMigrationRowStatus Status);

    private static string NormalizeId(string? value) => long.TryParse(value?.Trim(), out var id)
        ? id.ToString(System.Globalization.CultureInfo.InvariantCulture) : value?.Trim() ?? "";
}
