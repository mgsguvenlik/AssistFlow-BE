using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Data.Concrete.EfCore.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Model.Concrete.Collections;

internal static class CollectionUnknownContractIncluder
{
    private const string ResolutionNote =
        "Müşteri kararı 20.09.2026: boş veya Belirtilmemiş sözleşme durumu sözleşmesi var kabul edilerek tahsilata dahil edilir.";

    public static async Task RunAsync(string sourceSystem, string snapshotKey, byte[] expectedManifestHash,
        string settingsPath, string? expectedPlanHash)
    {
        await using var db = CreateContext(settingsPath, expectedPlanHash is null);
        await using var transaction = expectedPlanHash is null ? null
            : await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        if (transaction is not null)
            await db.Database.ExecuteSqlRawAsync("""
                DECLARE @result int;
                EXEC @result = sys.sp_getapplock @Resource=N'CollectionLegacyUnknownContractInclude',
                    @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=15000;
                IF @result < 0 THROW 51000, N'Belirtilmemiş sözleşme dahil etme kilidi alınamadı.', 1;
                """);

        var batch = await db.Set<CollectionMigrationBatch>().SingleAsync(x =>
            x.SourceSystem == sourceSystem && x.SnapshotKey == snapshotKey);
        if (!batch.ManifestHash.SequenceEqual(expectedManifestHash)
            || batch.Status != CollectionMigrationBatchStatus.NeedsReview)
            throw new InvalidOperationException("Kesit inceleme durumunda değil veya manifest değişmiş.");

        var existsStatusId = await db.Set<CollectionContractStatus>().AsNoTracking()
            .Where(x => x.Code == "EXISTS" && x.IsActive).Select(x => (long?)x.Id).SingleOrDefaultAsync()
            ?? throw new InvalidOperationException("Aktif VAR sözleşme durumu bulunamadı.");
        var candidates = await db.Set<CollectionMigrationContractStage>().Include(x => x.SourceRow)
            .Where(x => x.SourceRow.BatchId == batch.Id && x.Status == CollectionMigrationRowStatus.Blocked
                && (x.SourceContractStatusId == null || x.SourceContractStatusId == "14"))
            .OrderBy(x => x.SourceRow.SourceId).ToListAsync();
        var candidateRowIds = candidates.Select(x => x.SourceRowId).ToHashSet();
        var issues = await db.Set<CollectionMigrationIssue>()
            .Where(x => candidateRowIds.Contains(x.SourceRowId) && x.Status == CollectionMigrationIssueStatus.Open)
            .ToListAsync();
        var scopeIssues = issues.Where(x => x.IssueCode == "CONTRACT_NOT_INCLUDED").ToList();
        var otherIssueRows = issues.Where(x => x.IssueCode != "CONTRACT_NOT_INCLUDED")
            .Select(x => x.SourceRowId).ToHashSet();
        var ready = candidates.Where(x => !otherIssueRows.Contains(x.SourceRowId)).ToList();

        var material = string.Join('\n', candidates.Select(x =>
            $"C:{NormalizeId(x.SourceRow.SourceId)}:{Convert.ToHexString(x.SourceRow.PayloadHash)}"));
        var planHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
        Console.WriteLine($"Boş/Belirtilmemiş dahil etme planı: {candidates.Count} sözleşme; yalnız kapsam engeli bulunan {ready.Count}; başka istisnası bulunan {candidates.Count - ready.Count}.");
        Console.WriteLine($"Kapatılacak CONTRACT_NOT_INCLUDED issue: {scopeIssues.Count}.");
        Console.WriteLine($"Plan SHA-256: {planHash}");
        if (expectedPlanHash is null)
        {
            Console.WriteLine("DRY-RUN; staging, referans eşlemesi ve issue kayıtları değiştirilmedi.");
            return;
        }
        if (!planHash.Equals(expectedPlanHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Dahil etme planı değişmiş; işlem durduruldu. Güncel hash: {planHash}");

        var map14 = await db.Set<CollectionMigrationReferenceMap>().SingleAsync(x => x.BatchId == batch.Id
            && x.ReferenceKind == "ContractStatus" && x.SourceId == "14"
            && x.Status == CollectionMigrationDecisionStatus.Accepted);
        map14.TargetContractStatusId = existsStatusId;
        map14.MatchMethod = "ExplicitApproved";
        map14.Evidence = ResolutionNote;
        map14.DecidedDate = DateTimeOffset.UtcNow;
        map14.DecidedUser = null;
        var now = DateTimeOffset.UtcNow;
        foreach (var issue in scopeIssues)
        {
            issue.Status = CollectionMigrationIssueStatus.Resolved;
            issue.ResolutionNote = ResolutionNote;
            issue.ResolvedDate = now;
            issue.ResolvedUser = null;
        }
        foreach (var row in ready) row.Status = CollectionMigrationRowStatus.Pending;
        await db.SaveChangesAsync();
        await transaction!.CommitAsync();
        Console.WriteLine($"Karar uygulandı: {scopeIssues.Count} kapsam issue kapatıldı, {ready.Count} sözleşme aktarım adayı yapıldı; diğer istisnalı kayıtlar Blocked kaldı.");
    }

    private static AppDataContext CreateContext(string settingsPath, bool enableRetry)
    {
        using var settings = JsonDocument.Parse(File.ReadAllText(settingsPath), new JsonDocumentOptions
        { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        var connection = new SqlConnectionStringBuilder(settings.RootElement.GetProperty("AppSettings")
            .GetProperty("MSSQLConnectionString").GetString());
        if (connection.DataSource != "192.168.1.8" || connection.InitialCatalog != "AssistFlowTest")
            throw new InvalidOperationException("Belirtilmemiş sözleşme kararı yalnız AssistFlowTest üzerinde çalışır.");
        var options = new DbContextOptionsBuilder<AppDataContext>().UseSqlServer(connection.ConnectionString, sql =>
        { if (enableRetry) sql.EnableRetryOnFailure(); sql.CommandTimeout(180); }).Options;
        return new AppDataContext(options);
    }

    private static string NormalizeId(string value)
    {
        var trimmed = value.Trim();
        return long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
            ? id.ToString(CultureInfo.InvariantCulture) : trimmed;
    }
}
