using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Data.Concrete.EfCore.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Model.Concrete.Collections;

internal static class CollectionMigrationExceptionClassifier
{
    private const string ExclusionNote =
        "Müşteri kararı: legacy sözleşme durumu YOK olan kayıt tahsilat kapsamına alınmaz.";

    private static readonly IReadOnlyDictionary<string, string> Categories =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["CUSTOMER_ORPHAN"] = "Müşteri eşleşmesi",
            ["SUBSCRIBER_BLANK"] = "Müşteri eşleşmesi",
            ["SUBSCRIBER_TOO_LONG"] = "Müşteri eşleşmesi",
            ["SUBSCRIBER_SOURCE_DUPLICATE"] = "Müşteri eşleşmesi",
            ["CUSTOMER_TARGET_MISSING"] = "Müşteri eşleşmesi",
            ["CUSTOMER_TARGET_AMBIGUOUS"] = "Müşteri eşleşmesi",
            ["CUSTOMER_TARGET_DELETED"] = "Müşteri eşleşmesi",
            ["CUSTOMER_TYPE_MAP_MISSING"] = "Müşteri türü",
            ["CUSTOMER_TYPE_MISMATCH"] = "Müşteri türü",
            ["SERVICE_TYPE_MAP_MISSING"] = "Referans eşlemesi",
            ["CURRENCY_MAP_MISSING"] = "Referans eşlemesi",
            ["CONTRACT_STATUS_MAP_MISSING"] = "Referans eşlemesi",
            ["SUBSCRIPTION_STATUS_MAP_MISSING"] = "Referans eşlemesi",
            ["PAYMENT_METHOD_MAP_MISSING"] = "Referans eşlemesi",
            ["PAYMENT_FREQUENCY_MAP_MISSING"] = "Referans eşlemesi",
            ["CONTRACT_NOT_INCLUDED"] = "Kapsam kararı",
            ["SUBSCRIPTION_STATUS_UNKNOWN"] = "Kapsam kararı",
            ["DATE_INVALID"] = "Tarih/veri kalitesi",
            ["RANGE_REVERSED"] = "Tarih/veri kalitesi",
            ["PERIOD_START_DUPLICATE"] = "Tarife dönem bütünlüğü",
            ["PERIOD_OVERLAP"] = "Tarife dönem bütünlüğü",
            ["MULTIPLE_OPEN_PERIODS"] = "Tarife dönem bütünlüğü",
            ["HISTORY_MISSING"] = "Tarife dönem bütünlüğü",
            ["HISTORY_INVALID"] = "Tarife dönem bütünlüğü",
            ["CONTRACT_PARENT_MISSING"] = "Tarife dönem bütünlüğü",
            ["HISTORY_CUSTOMER_MISMATCH"] = "Tarife dönem bütünlüğü",
            ["CONTRACT_CURRENT_RATE_MISMATCH"] = "Tarife dönem bütünlüğü",
            ["AMOUNT_INVALID"] = "Finansal veri kalitesi",
            ["PROCESS_TYPE_UNKNOWN"] = "Süreç türü",
            ["ATTACHMENT_METADATA_TOO_LONG"] = "Dosya metadata",
            ["ATTACHMENT_METADATA_INCOMPLETE"] = "Dosya metadata"
        };

    public static async Task RunAsync(string sourceSystem, string snapshotKey, byte[] expectedManifestHash,
        string settingsPath, string? expectedPlanHash)
    {
        await using var db = CreateContext(settingsPath, expectedPlanHash is null);
        await using var transaction = expectedPlanHash is null
            ? null
            : await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        if (transaction is not null)
            await db.Database.ExecuteSqlRawAsync("""
                DECLARE @result int;
                EXEC @result = sys.sp_getapplock @Resource=N'CollectionLegacyContractExclusion',
                    @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=15000;
                IF @result < 0 THROW 51000, N'Legacy sözleşme hariç tutma kilidi alınamadı.', 1;
                """);

        var batch = await db.Set<CollectionMigrationBatch>().SingleOrDefaultAsync(x =>
            x.SourceSystem == sourceSystem && x.SnapshotKey == snapshotKey)
            ?? throw new InvalidOperationException("Kesit staging kaydı bulunamadı.");
        if (!batch.ManifestHash.SequenceEqual(expectedManifestHash))
            throw new InvalidOperationException("Staging manifest hash'i kesitle uyuşmuyor.");
        if (batch.Status != CollectionMigrationBatchStatus.NeedsReview)
            throw new InvalidOperationException("İstisna sınıflandırması yalnız inceleme bekleyen kesitte çalışır.");

        var noneStatusId = await db.Set<CollectionContractStatus>().AsNoTracking()
            .Where(x => x.Code == "NONE" && x.IsActive).Select(x => (long?)x.Id).SingleOrDefaultAsync()
            ?? throw new InvalidOperationException("Aktif YOK sözleşme durumu bulunamadı.");
        var noneSourceIds = (await db.Set<CollectionMigrationReferenceMap>().AsNoTracking()
                .Where(x => x.BatchId == batch.Id && x.ReferenceKind == "ContractStatus"
                    && x.Status == CollectionMigrationDecisionStatus.Accepted
                    && x.TargetContractStatusId == noneStatusId)
                .Select(x => x.SourceId).ToListAsync())
            .Select(NormalizeId).ToHashSet(StringComparer.Ordinal);
        if (noneSourceIds.Count == 0)
            throw new InvalidOperationException("YOK için onaylanmış kaynak sözleşme durumu eşlemesi bulunamadı.");

        var mappedContractIds = (await db.Set<CollectionMigrationMap>().AsNoTracking()
                .Where(x => x.SourceSystem == sourceSystem && x.EntityCode == "Contract")
                .Select(x => x.SourceId).ToListAsync())
            .Select(NormalizeId).ToHashSet(StringComparer.Ordinal);
        var candidates = await db.Set<CollectionMigrationContractStage>().Include(x => x.SourceRow)
            .Where(x => x.SourceRow.BatchId == batch.Id
                && x.Status != CollectionMigrationRowStatus.Applied
                && x.Status != CollectionMigrationRowStatus.AlreadyApplied
                && x.Status != CollectionMigrationRowStatus.Excluded)
            .ToListAsync();
        candidates = candidates.Where(x => x.SourceContractStatusId is not null
                && noneSourceIds.Contains(NormalizeId(x.SourceContractStatusId))
                && !mappedContractIds.Contains(NormalizeId(x.SourceRow.SourceId)))
            .OrderBy(x => NormalizeId(x.SourceRow.SourceId), StringComparer.Ordinal).ToList();

        var candidateIds = candidates.Select(x => NormalizeId(x.SourceRow.SourceId))
            .ToHashSet(StringComparer.Ordinal);
        var histories = await db.Set<CollectionMigrationRatePeriodStage>().Include(x => x.SourceRow)
            .Where(x => x.SourceRow.BatchId == batch.Id
                && x.Status != CollectionMigrationRowStatus.Applied
                && x.Status != CollectionMigrationRowStatus.AlreadyApplied
                && x.Status != CollectionMigrationRowStatus.Excluded)
            .ToListAsync();
        histories = histories.Where(x => x.SourceContractId is not null
                && candidateIds.Contains(NormalizeId(x.SourceContractId)))
            .OrderBy(x => NormalizeId(x.SourceContractId!), StringComparer.Ordinal)
            .ThenBy(x => NormalizeId(x.SourceRow.SourceId), StringComparer.Ordinal).ToList();

        var material = string.Join('\n', candidates.Select(x =>
                $"C:{NormalizeId(x.SourceRow.SourceId)}:{Convert.ToHexString(x.SourceRow.PayloadHash)}")
            .Concat(histories.Select(x =>
                $"H:{NormalizeId(x.SourceRow.SourceId)}:{Convert.ToHexString(x.SourceRow.PayloadHash)}")));
        var planHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
        Console.WriteLine($"YOK hariç tutma planı: {candidates.Count} sözleşme, {histories.Count} tarife dönemi.");
        Console.WriteLine($"Plan SHA-256: {planHash}");

        if (expectedPlanHash is null)
        {
            await PrintRemainingIssuesAsync(db, batch.Id, candidateIds);
            Console.WriteLine("DRY-RUN; staging durumu ve issue kayıtları değiştirilmedi.");
            return;
        }
        if (!planHash.Equals(expectedPlanHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Hariç tutma planı değişmiş; işlem durduruldu. Güncel hash: {planHash}");

        var excludedRowIds = candidates.Select(x => x.SourceRowId)
            .Concat(histories.Select(x => x.SourceRowId)).ToHashSet();
        var now = DateTimeOffset.UtcNow;
        foreach (var row in candidates) row.Status = CollectionMigrationRowStatus.Excluded;
        foreach (var row in histories) row.Status = CollectionMigrationRowStatus.Excluded;
        var issues = await db.Set<CollectionMigrationIssue>()
            .Where(x => excludedRowIds.Contains(x.SourceRowId)
                && x.Status == CollectionMigrationIssueStatus.Open).ToListAsync();
        foreach (var issue in issues)
        {
            issue.Status = CollectionMigrationIssueStatus.Ignored;
            issue.ResolutionNote = ExclusionNote;
            issue.ResolvedDate = now;
            issue.ResolvedUser = null;
        }
        await db.SaveChangesAsync();
        await transaction!.CommitAsync();
        Console.WriteLine($"YOK kapsam kararı uygulandı: {candidates.Count} sözleşme ve {histories.Count} tarife dönemi Excluded; {issues.Count} açık issue audit notuyla kapatıldı.");

        await using var reportDb = CreateContext(settingsPath, true);
        await PrintRemainingIssuesAsync(reportDb, batch.Id, []);
    }

    private static async Task PrintRemainingIssuesAsync(AppDataContext db, long batchId,
        HashSet<string> plannedExcludedContractIds)
    {
        var rows = await db.Set<CollectionMigrationIssue>().AsNoTracking()
            .Where(x => x.SourceRow.BatchId == batchId && x.Status == CollectionMigrationIssueStatus.Open)
            .Select(x => new
            {
                x.IssueCode,
                x.SourceRow.EntityCode,
                x.SourceRow.SourceId,
                x.SourceRow.SourceParentId
            })
            .ToListAsync();
        if (plannedExcludedContractIds.Count > 0)
            rows = rows.Where(x =>
                    (x.EntityCode != "Contract"
                        || !plannedExcludedContractIds.Contains(NormalizeId(x.SourceId)))
                    && (x.EntityCode != "ContractHistory" || x.SourceParentId is null
                        || !plannedExcludedContractIds.Contains(NormalizeId(x.SourceParentId))))
                .ToList();
        Console.WriteLine("Kalan açık istisna sınıfları:");
        foreach (var group in rows.GroupBy(x => new
                 {
                     Category = Categories.GetValueOrDefault(x.IssueCode, "Diğer"),
                     x.EntityCode,
                     x.IssueCode
                 }).OrderByDescending(x => x.Count()).ThenBy(x => x.Key.Category)
                 .ThenBy(x => x.Key.EntityCode).ThenBy(x => x.Key.IssueCode))
            Console.WriteLine($"  {group.Key.Category}|{group.Key.EntityCode}|{group.Key.IssueCode}|{group.Count()}");

        var openContractIssueIds = await db.Set<CollectionMigrationIssue>().AsNoTracking()
            .Where(x => x.SourceRow.BatchId == batchId && x.Status == CollectionMigrationIssueStatus.Open
                && x.IssueCode == "CONTRACT_NOT_INCLUDED")
            .Select(x => x.SourceRowId).ToListAsync();
        var contractStatusBreakdown = await db.Set<CollectionMigrationContractStage>().AsNoTracking()
            .Where(x => openContractIssueIds.Contains(x.SourceRowId))
            .GroupBy(x => x.SourceContractStatusId)
            .Select(x => new { SourceId = x.Key, Count = x.Count() })
            .OrderByDescending(x => x.Count).ThenBy(x => x.SourceId).ToListAsync();
        Console.WriteLine("Kalan CONTRACT_NOT_INCLUDED kaynak durumları:");
        foreach (var item in contractStatusBreakdown)
            Console.WriteLine($"  {(string.IsNullOrWhiteSpace(item.SourceId) ? "<BOŞ>" : item.SourceId.Trim())}|{item.Count}");

        var unknownProcessPayloads = await db.Set<CollectionMigrationIssue>().AsNoTracking()
            .Where(x => x.SourceRow.BatchId == batchId && x.Status == CollectionMigrationIssueStatus.Open
                && x.IssueCode == "PROCESS_TYPE_UNKNOWN")
            .Select(x => x.SourceRow.Payload).ToListAsync();
        Console.WriteLine("Kalan PROCESS_TYPE_UNKNOWN kaynak değerleri:");
        foreach (var item in unknownProcessPayloads.Select(ProcessType)
                     .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
                     .OrderByDescending(x => x.Count()).ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            Console.WriteLine($"  {item.Key}|{item.Count()}");
    }

    private static string ProcessType(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        if (!document.RootElement.TryGetProperty("ProcessType", out var value)
            || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return "<BOŞ>";
        var text = value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
        return string.IsNullOrWhiteSpace(text) ? "<BOŞ>" : text.Trim();
    }

    private static AppDataContext CreateContext(string settingsPath, bool enableRetry)
    {
        using var settings = JsonDocument.Parse(File.ReadAllText(settingsPath), new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        });
        var connection = new SqlConnectionStringBuilder(settings.RootElement.GetProperty("AppSettings")
            .GetProperty("MSSQLConnectionString").GetString());
        if (connection.DataSource != "192.168.1.8" || connection.InitialCatalog != "AssistFlowTest")
            throw new InvalidOperationException("İstisna sınıflandırması yalnız AssistFlowTest üzerinde çalışır.");
        var options = new DbContextOptionsBuilder<AppDataContext>().UseSqlServer(connection.ConnectionString, sql =>
        {
            if (enableRetry) sql.EnableRetryOnFailure();
            sql.CommandTimeout(180);
        }).Options;
        return new AppDataContext(options);
    }

    private static string NormalizeId(string value)
    {
        var trimmed = value.Trim();
        return long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
            ? id.ToString(CultureInfo.InvariantCulture) : trimmed;
    }
}
