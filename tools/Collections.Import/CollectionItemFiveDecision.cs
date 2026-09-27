using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Data.Concrete.EfCore.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Model.Concrete.Collections;

/// <summary>Yalnız kontrol edilmiş üç literal-null tarihçe için müşteri kararını kaydeder.</summary>
internal static class CollectionItemFiveDecision
{
    internal const string WorkbookHash = "2CA674418FE29BEDCC4895A1172883D9D733646FC4709E8BA8142B9A13CC5315";
    internal const string ApprovalPrefix = "26.09.2026 madde 5 net kayıt: ";
    internal static readonly string[] ContractIds = ["27590", "48715", "49221"];
    private static readonly Dictionary<string, (string History, int Row)> Decisions = new()
    {
        ["27590"] = ("34133", 7), ["48715"] = ("82291", 22), ["49221"] = ("84457", 23)
    };

    public static async Task RunAsync(long batchId, string settingsPath, string workbookPath, string? expectedHash)
    {
        if (Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(workbookPath))) != WorkbookHash)
            throw new InvalidOperationException("Müşteri karar dosyası incelenen sürümle aynı değil.");
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath), new JsonDocumentOptions
        { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        var connection = new SqlConnectionStringBuilder(settings.RootElement.GetProperty("AppSettings").GetProperty("MSSQLConnectionString").GetString());
        if (connection.DataSource != "192.168.1.8" || connection.InitialCatalog != "AssistFlowTest" || batchId != 1)
            throw new InvalidOperationException("Madde 5 kararı yalnız incelenen AssistFlowTest kesitine uygulanır.");
        await using var db = new AppDataContext(new DbContextOptionsBuilder<AppDataContext>()
            .UseSqlServer(connection.ConnectionString, sql => sql.CommandTimeout(180)).Options);
        await using var tx = expectedHash is null ? null : await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        if (tx is not null)
            await db.Database.ExecuteSqlRawAsync("""
                DECLARE @result int;
                EXEC @result = sys.sp_getapplock @Resource=N'CollectionLegacyContractApply',
                    @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=15000;
                IF @result<0 THROW 51000, N'Aktarım karar kilidi alınamadı.', 1;
                """);
        var batch = await db.Set<CollectionMigrationBatch>().AsNoTracking().SingleAsync(x => x.Id == batchId);
        Require(batch.SourceSystem == "MGS" && batch.SnapshotKey == "mgs-20260919-131119Z"
            && batch.Status == CollectionMigrationBatchStatus.NeedsReview, "Kesit kimliği veya durumu değişmiş.");
        var contracts = await db.Set<CollectionMigrationContractStage>().Include(x => x.SourceRow)
            .Where(x => x.SourceRow.BatchId == batchId && ContractIds.Contains(x.SourceRow.SourceId)).OrderBy(x => x.SourceRowId).ToListAsync();
        var rates = await db.Set<CollectionMigrationRatePeriodStage>().Include(x => x.SourceRow)
            .Where(x => x.SourceRow.BatchId == batchId && x.SourceContractId != null && ContractIds.Contains(x.SourceContractId.Trim()))
            .OrderBy(x => x.SourceRowId).ToListAsync();
        Require(contracts.Count == 3 && rates.Count == 13, "Onaylı sözleşme/tarihçe kümesi değişmiş.");
        var sourceRows = contracts.Select(x => x.SourceRow).Concat(rates.Select(x => x.SourceRow)).ToList();
        foreach (var row in sourceRows)
            Require(SHA256.HashData(Encoding.UTF8.GetBytes(row.Payload)).SequenceEqual(row.PayloadHash), "Ham kaynak hash'i değişmiş.");
        var rowIds = sourceRows.Select(x => x.Id).ToArray();
        var issues = await db.Set<CollectionMigrationIssue>().Where(x => rowIds.Contains(x.SourceRowId)).OrderBy(x => x.Id).ToListAsync();
        var changes = new List<(CollectionMigrationContractStage Contract, CollectionMigrationRatePeriodStage Rate)>();
        foreach (var contract in contracts)
        {
            var decision = Decisions[contract.SourceRow.SourceId];
            var history = rates.Single(x => x.SourceRow.SourceId == decision.History);
            using var payload = JsonDocument.Parse(history.SourceRow.Payload);
            Require(payload.RootElement.GetProperty("ProcessType").ValueKind == JsonValueKind.String
                && payload.RootElement.GetProperty("ProcessType").GetString()?.Trim() == "null", "Karar literal null işlem türüne ait değil.");
            Require(CollectionTransferPlan.NormalizeId(history.SourceContractId) == contract.SourceRow.SourceId
                && CollectionTransferPlan.NormalizeId(history.SourceCustomerId) == CollectionTransferPlan.NormalizeId(contract.SourceCustomerId), "Tarihçe/sözleşme/müşteri ilişkisi değişmiş.");
            var audit = issues.SingleOrDefault(x => x.SourceRowId == history.SourceRowId && x.IssueCode == "PROCESS_TYPE_CUSTOMER_APPROVED"
                && x.Status == CollectionMigrationIssueStatus.Resolved && x.ResolutionNote != null
                && x.ResolutionNote.StartsWith(ApprovalPrefix) && x.ResolutionNote.Contains(WorkbookHash)
                && x.ResolutionNote.Contains(Convert.ToHexString(history.SourceRow.PayloadHash)));
            if (audit is not null)
            {
                Require(history.BillingBehavior == CollectionBillingBehavior.Billable
                    && history.Status is CollectionMigrationRowStatus.Pending or CollectionMigrationRowStatus.Applied
                    && contract.Status is CollectionMigrationRowStatus.Pending or CollectionMigrationRowStatus.Applied,
                    "Önceki karar sonrası durum değişmiş.");
                continue;
            }
            Require(contract.Status == CollectionMigrationRowStatus.Blocked && history.Status == CollectionMigrationRowStatus.Blocked
                && history.BillingBehavior == null, "Beklenen karar öncesi durum bulunamadı.");
            var histories = rates.Where(x => CollectionTransferPlan.NormalizeId(x.SourceContractId) == contract.SourceRow.SourceId).ToList();
            Require(histories.Where(x => x.SourceRowId != history.SourceRowId).All(x => x.Status == CollectionMigrationRowStatus.Pending), "Başka tarihçe engeli mevcut.");
            var scopeIds = histories.Select(x => x.SourceRowId).Append(contract.SourceRowId).ToHashSet();
            var open = issues.Where(x => scopeIds.Contains(x.SourceRowId) && x.Status == CollectionMigrationIssueStatus.Open).ToList();
            Require(open.Count == 2 && open.Count(x => x.SourceRowId == history.SourceRowId && x.IssueCode == "PROCESS_TYPE_UNKNOWN") == 1
                && open.Count(x => x.SourceRowId == contract.SourceRowId && x.IssueCode == "HISTORY_INVALID") == 1,
                "İşlem türü dışında karar gerektiren engel var.");
            changes.Add((contract, history));
        }
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            WorkbookHash, batch.Id, batch.ManifestHash,
            Sources = sourceRows.Select(x => new { x.Id, x.PayloadHash }),
            Contracts = contracts.Select(x => new { x.SourceRowId, x.RowVersion, x.Status }),
            Rates = rates.Select(x => new { x.SourceRowId, x.RowVersion, x.Status, x.BillingBehavior }),
            Issues = issues.Select(x => new { x.Id, x.Status, x.ResolutionNote })
        }))));
        Console.WriteLine($"Madde 5 karar önizlemesi: yeni karar={changes.Count}; kapsamdaki sözleşme=3; tarihçe=13; SHA-256: {hash}");
        if (expectedHash is null || changes.Count == 0) { Console.WriteLine("Veri değiştirilmedi."); return; }
        Require(hash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase), "Karar planı değişmiş; tekrar önizleme gerekir.");
        var now = DateTimeOffset.UtcNow;
        foreach (var (contract, history) in changes)
        {
            var note = $"{ApprovalPrefix}KAYIT OLDUĞU GİBİ KALSIN. Literal null legacy ücretli dönem davranışı korunur; tutar/tarih değiştirilmez. Sayfa=5 - Null İşlem Türü; hücre=H{Decisions[contract.SourceRow.SourceId].Row}; workbook SHA-256={WorkbookHash}; kaynak SHA-256={Convert.ToHexString(history.SourceRow.PayloadHash)}.";
            history.BillingBehavior = CollectionBillingBehavior.Billable;
            history.Status = CollectionMigrationRowStatus.Pending;
            contract.Status = CollectionMigrationRowStatus.Pending;
            foreach (var issue in issues.Where(x => x.Status == CollectionMigrationIssueStatus.Open
                && (x.SourceRowId == history.SourceRowId || x.SourceRowId == contract.SourceRowId)))
            {
                issue.Status = CollectionMigrationIssueStatus.Resolved;
                issue.ResolutionNote = note;
                issue.ResolvedDate = now;
            }
            db.Set<CollectionMigrationIssue>().Add(new() { SourceRowId = history.SourceRowId, IssueCode = "PROCESS_TYPE_CUSTOMER_APPROVED",
                Severity = CollectionMigrationIssueSeverity.Warning, Status = CollectionMigrationIssueStatus.Resolved,
                Details = note, ResolutionNote = note, RuleVersion = batch.RuleVersion, CreatedDate = now, ResolvedDate = now });
        }
        await db.SaveChangesAsync();
        await tx!.CommitAsync();
        Console.WriteLine($"COMMIT: {changes.Count} satırın müşteri kararı staging'e işlendi. Hedef aktarımı henüz yapılmadı.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
