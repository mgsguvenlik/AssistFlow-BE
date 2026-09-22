using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Data.Concrete.EfCore.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Model.Concrete.Collections;

internal static class CollectionUnidentifiedCustomerExcluder
{
    private const string ResolutionNote =
        "Müşteri kararı 20.09.2026: hem abone numarası hem müşteri adı bulunmayan kayıt dikkate alınmaz.";

    public static async Task RunAsync(string sourceSystem, string snapshotKey, byte[] expectedManifestHash,
        string settingsPath, string? expectedPlanHash)
    {
        await using var db = CreateContext(settingsPath, expectedPlanHash is null);
        await using var transaction = expectedPlanHash is null ? null
            : await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        if (transaction is not null)
            await db.Database.ExecuteSqlRawAsync("""
                DECLARE @result int;
                EXEC @result = sys.sp_getapplock @Resource=N'CollectionLegacyUnidentifiedCustomerExclude',
                    @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=15000;
                IF @result < 0 THROW 51000, N'Kimliği belirsiz müşteri dışlama kilidi alınamadı.', 1;
                """);

        var batch = await db.Set<CollectionMigrationBatch>().SingleAsync(x =>
            x.SourceSystem == sourceSystem && x.SnapshotKey == snapshotKey);
        if (!batch.ManifestHash.SequenceEqual(expectedManifestHash)
            || batch.Status != CollectionMigrationBatchStatus.NeedsReview)
            throw new InvalidOperationException("Kesit inceleme durumunda değil veya manifest değişmiş.");

        var sourceRows = await db.Set<CollectionMigrationSourceRow>().AsNoTracking()
            .Where(x => x.BatchId == batch.Id && x.EntityCode == "Customer")
            .Select(x => new { x.SourceId, x.Payload }).ToListAsync();
        var identifiedCustomerIds = sourceRows.Where(x =>
        {
            using var document = JsonDocument.Parse(x.Payload);
            return !string.IsNullOrWhiteSpace(Text(document.RootElement, "SubscriberNo"))
                || !string.IsNullOrWhiteSpace(Text(document.RootElement, "Name"));
        }).Select(x => NormalizeId(x.SourceId)).ToHashSet(StringComparer.Ordinal);

        var mappedContractIds = (await db.Set<CollectionMigrationMap>().AsNoTracking()
            .Where(x => x.SourceSystem == sourceSystem && x.EntityCode == "Contract")
            .Select(x => x.SourceId).ToListAsync()).Select(NormalizeId).ToHashSet(StringComparer.Ordinal);
        var candidates = await db.Set<CollectionMigrationContractStage>().Include(x => x.SourceRow)
            .Where(x => x.SourceRow.BatchId == batch.Id
                && x.Status != CollectionMigrationRowStatus.Applied
                && x.Status != CollectionMigrationRowStatus.AlreadyApplied
                && x.Status != CollectionMigrationRowStatus.Excluded)
            .ToListAsync();
        candidates = candidates.Where(x => !mappedContractIds.Contains(NormalizeId(x.SourceRow.SourceId))
                && !identifiedCustomerIds.Contains(NormalizeId(x.SourceCustomerId)))
            .OrderBy(x => NormalizeId(x.SourceRow.SourceId), StringComparer.Ordinal).ToList();
        var contractIds = candidates.Select(x => NormalizeId(x.SourceRow.SourceId)).ToHashSet(StringComparer.Ordinal);
        var histories = await db.Set<CollectionMigrationRatePeriodStage>().Include(x => x.SourceRow)
            .Where(x => x.SourceRow.BatchId == batch.Id
                && x.Status != CollectionMigrationRowStatus.Applied
                && x.Status != CollectionMigrationRowStatus.AlreadyApplied
                && x.Status != CollectionMigrationRowStatus.Excluded)
            .ToListAsync();
        histories = histories.Where(x => x.SourceContractId is not null
                && contractIds.Contains(NormalizeId(x.SourceContractId)))
            .OrderBy(x => NormalizeId(x.SourceContractId!), StringComparer.Ordinal)
            .ThenBy(x => NormalizeId(x.SourceRow.SourceId), StringComparer.Ordinal).ToList();

        var material = string.Join('\n', candidates.Select(x =>
                $"C:{NormalizeId(x.SourceRow.SourceId)}:{Convert.ToHexString(x.SourceRow.PayloadHash)}")
            .Concat(histories.Select(x =>
                $"H:{NormalizeId(x.SourceRow.SourceId)}:{Convert.ToHexString(x.SourceRow.PayloadHash)}")));
        var planHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
        Console.WriteLine($"Kimliği belirsiz müşteri dışlama planı: {candidates.Count} sözleşme, {histories.Count} tarife dönemi.");
        Console.WriteLine($"Plan SHA-256: {planHash}");
        if (expectedPlanHash is null)
        {
            Console.WriteLine("DRY-RUN; staging durumu ve issue kayıtları değiştirilmedi.");
            return;
        }
        if (!planHash.Equals(expectedPlanHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Dışlama planı değişmiş; işlem durduruldu. Güncel hash: {planHash}");

        var rowIds = candidates.Select(x => x.SourceRowId).Concat(histories.Select(x => x.SourceRowId)).ToHashSet();
        foreach (var row in candidates) row.Status = CollectionMigrationRowStatus.Excluded;
        foreach (var row in histories) row.Status = CollectionMigrationRowStatus.Excluded;
        var issues = await db.Set<CollectionMigrationIssue>()
            .Where(x => rowIds.Contains(x.SourceRowId) && x.Status == CollectionMigrationIssueStatus.Open).ToListAsync();
        var now = DateTimeOffset.UtcNow;
        foreach (var issue in issues)
        {
            issue.Status = CollectionMigrationIssueStatus.Ignored;
            issue.ResolutionNote = ResolutionNote;
            issue.ResolvedDate = now;
            issue.ResolvedUser = null;
        }
        await db.SaveChangesAsync();
        await transaction!.CommitAsync();
        Console.WriteLine($"Karar uygulandı: {candidates.Count} sözleşme ve {histories.Count} tarife dönemi dışlandı; {issues.Count} açık issue audit notuyla kapatıldı.");
    }

    private static string? Text(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var value)
            || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;
        var text = value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    private static AppDataContext CreateContext(string settingsPath, bool enableRetry)
    {
        using var settings = JsonDocument.Parse(File.ReadAllText(settingsPath), new JsonDocumentOptions
        { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        var connection = new SqlConnectionStringBuilder(settings.RootElement.GetProperty("AppSettings")
            .GetProperty("MSSQLConnectionString").GetString());
        if (connection.DataSource != "192.168.1.8" || connection.InitialCatalog != "AssistFlowTest")
            throw new InvalidOperationException("Kimliği belirsiz müşteri kararı yalnız AssistFlowTest üzerinde çalışır.");
        var options = new DbContextOptionsBuilder<AppDataContext>().UseSqlServer(connection.ConnectionString, sql =>
        { if (enableRetry) sql.EnableRetryOnFailure(); sql.CommandTimeout(180); }).Options;
        return new AppDataContext(options);
    }

    private static string NormalizeId(string? value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        return long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
            ? id.ToString(CultureInfo.InvariantCulture) : trimmed;
    }
}
