using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Business.Services.Crm.Collections;
using Business.Services.Crm.Collections.Calculation;
using Data.Concrete.EfCore.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Model.Concrete;
using Model.Concrete.Collections;
using Model.Dtos.Crm.Collections;

using static CollectionTransferCore;

// Deliberately separate from the production preparation tool and test-only importers.
// No source database connection, sender, storage provider, application startup or migration call.
internal static class RehearsalTransfer
{
    private const string Target = "AssistFlow_Performance_Restore";
    private const string Rule = "rehearsal-receipt-v1";
    private static async Task<AppDataContext> Open(string config)
    {
        using var cfg = JsonDocument.Parse(await File.ReadAllTextAsync(config), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var text = Environment.GetEnvironmentVariable("COLLECTION_RELEASE_CONNECTIONSTRING") ?? cfg.RootElement.GetProperty("AppSettings").GetProperty("MSSQLConnectionString").GetString();
        var b = new SqlConnectionStringBuilder(text); Require(b.InitialCatalog == "AssistFlow", "Kaynak config beklenenden farklı; başka DB seçilemez.");
        b.InitialCatalog = Target; b.ApplicationName = "CollectionRehearsalOnly"; b.ConnectTimeout = 10;
        var db = new AppDataContext(new DbContextOptionsBuilder<AppDataContext>().UseSqlServer(b.ConnectionString, x => x.CommandTimeout(300)).Options);
        await db.Database.OpenConnectionAsync();
        await db.Database.ExecuteSqlRawAsync("""
            IF DB_NAME()<>N'AssistFlow_Performance_Restore' OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'sql2022' THROW 51000,N'Yalnız onaylı prova hedefi kullanılabilir.',1;
            IF EXISTS(SELECT 1 FROM sys.triggers WHERE is_disabled=0) OR EXISTS(SELECT 1 FROM sys.server_triggers WHERE is_disabled=0) THROW 51000,N'Etkin trigger; prova durduruldu.',1;
            IF EXISTS(SELECT 1 FROM sys.databases WHERE database_id=DB_ID() AND (is_trustworthy_on=1 OR is_db_chaining_on=1 OR is_broker_enabled=1)) THROW 51000,N'Prova güvenlik ayarı uygun değil.',1;
            """);
        Require(!(await db.Database.GetPendingMigrationsAsync()).Any(), "Prova şeması eksik.");
        Require(!await db.Users.AnyAsync(x => x.Id == 0), "Sistem aktörü gerçek kullanıcıyla çakışıyor.");
        return db;
    }
    private static Input Load(string capture, string review, string payments, string relations)
    {
        var p = new Input { Capture = Private(capture), Review = Private(review), Payments = Private(payments), Relations = Private(relations) };
        foreach (var root in new[] { p.Capture, p.Review, p.Payments, p.Relations }) VerifyPackage(root);
        var m = Doc(Path.Combine(p.Capture, "capture-manifest.json"));
        Require(T(m, "TargetDatabase") == Target && m.GetProperty("RehearsalOnly").GetBoolean() && m.GetProperty("AllObservedHashesStable").GetBoolean(), "Prova için sabit ve doğru hedefli capture gereklidir.");
        var reviewHash = HashFile(Path.Combine(p.Review, "package-hashes.json"));
        foreach (var root in new[] { p.Payments, p.Relations }) { var s = Doc(Path.Combine(root, "summary.json")); Require(T(s, "CapturePackageHash") == p.CaptureHash && T(s, "ReviewPackageHash") == reviewHash && !s.GetProperty("ReadyToApply").GetBoolean(), "Ödeme/ilişki kaynak zinciri uyuşmuyor."); }
        Require(Path.GetFullPath(T(Doc(Path.Combine(p.Review, "summary.json")), "SourceCapturePath")) == p.Capture, "İnceleme capture ilişkisi farklı.");
        foreach (var c in p.Contracts)
        {
            Require(c.GetProperty("Issues").GetArrayLength() == 0 && c.GetProperty("Rates").GetArrayLength() > 0 && N(c, "TargetCustomerId") > 0, "Sözleşme aday kapısı geçersiz.");
            Require(c.GetProperty("Gates").EnumerateArray().All(x => x.GetString() is "SERVICE_INSERT_REQUIRES_APPROVAL" or "CUSTOMER_TYPE_UPDATE_REQUIRES_APPROVAL"), "Desteklenmeyen aday kapısı.");
        }
        return p;
    }
    public static async Task PlanAsync(string config, string capture, string review, string payments, string relations, string output)
    {
        var p = Load(capture, review, payments, relations); await using var db = await Open(config);
        Require(!await db.Set<CollectionMigrationBatch>().AnyAsync() && !await db.Set<CollectionContract>().AnyAsync() && !await db.Set<CollectionPayment>().AnyAsync(), "İlk plan yalnız boş Tahsilat veri hedefinde hazırlanır.");
        foreach (var (file, sql) in ReferenceQueries) Require(await Fingerprint(db, sql) == HashFile(Path.Combine(p.Capture, file)), "Hedef referanslar capture sonrasında değişmiş: " + file);
        var value = new { Version = Rule, Target, Server = "sql2022", p.Capture, p.Review, p.Payments, p.Relations, p.CaptureHash,
            ReviewHash = HashFile(Path.Combine(p.Review, "package-hashes.json")), PaymentsHash = HashFile(Path.Combine(p.Payments, "package-hashes.json")), RelationsHash = HashFile(Path.Combine(p.Relations, "package-hashes.json")),
            Contracts = p.Contracts.Length, Rates = p.Contracts.Sum(x => x.GetProperty("Rates").GetArrayLength()), PaymentCount = N(Doc(Path.Combine(p.Payments, "summary.json")), "Candidates"),
            TechnicalActor = 0, OutboundProviders = "NOT_INSTANTIATED_OR_CALLED", Files = "LOCAL_HASH_AUDIT_ONLY_NO_CDN_OR_FAKE_ATTACHMENT", ProductionApplyAllowed = false };
        var root = NewOutput(output); await Save(root, "plan.json", value);
        Console.WriteLine($"Prova planı: {value.Contracts} sözleşme, {value.Rates} tarife, {value.PaymentCount} ödeme. SHA-256: {HashFile(Path.Combine(root, "plan.json"))}. Veri yazılmadı.");
    }
    public static async Task RunAsync(string mode, string config, string planRoot, string expectedHash, string output, string acknowledgement)
    {
        Require(acknowledgement == "REHEARSAL_ONLY", "Yalnız prova teyidi gereklidir."); planRoot = Private(planRoot);
        Require(HashFile(Path.Combine(planRoot, "plan.json")) == expectedHash, "Onaylı plan hash'i farklı."); var plan = Doc(Path.Combine(planRoot, "plan.json"));
        Require(T(plan, "Version") == Rule && T(plan, "Target") == Target && !plan.GetProperty("ProductionApplyAllowed").GetBoolean(), "Prova planı hedef/sürüm hatası.");
        var p = Load(T(plan, "Capture"), T(plan, "Review"), T(plan, "Payments"), T(plan, "Relations"));
        Require(T(plan, "CaptureHash") == p.CaptureHash && T(plan, "ReviewHash") == HashFile(Path.Combine(p.Review, "package-hashes.json"))
            && T(plan, "PaymentsHash") == HashFile(Path.Combine(p.Payments, "package-hashes.json")) && T(plan, "RelationsHash") == HashFile(Path.Combine(p.Relations, "package-hashes.json")), "Plan kaynağı değişmiş.");
        var root = NewOutput(output); await using var db = await Open(config);
        if (mode == "resilience-check") { await ResilienceAsync(db, p, root, Rule + "-" + p.CaptureHash); return; }
        var before = await StateAsync(db);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'CollectionRehearsalApply.v1',@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=15000; IF @r<0 THROW 51000,N'Prova kilidi alınamadı.',1;");
            var snapshot = Rule + "-" + p.CaptureHash;
            var existing = await db.Set<CollectionMigrationBatch>().AsNoTracking().SingleOrDefaultAsync(x => x.SourceSystem == "MGS" && x.SnapshotKey == snapshot);
            if (existing is not null)
            {
                Require(existing.ManifestHash.SequenceEqual(Convert.FromHexString(p.CaptureHash)) && existing.RuleVersion == Rule && existing.Status == CollectionMigrationBatchStatus.Reconciled, "Yarım/farklı prova batch'i otomatik uygulanmaz.");
                Require(mode != "rollback-check", "Yarım işlem kontrolü ilk importtan önce yapılmalıdır.");
                var replay = await VerifyAsync(db, p, existing.Id, true); await tx.RollbackAsync();
                await Save(root, "result.json", new { Mode = "IDEMPOTENT_REPLAY", Inserted = 0, Verification = replay, Before = before, After = await StateAsync(db), ProductionWrites = false });
                Console.WriteLine("Tekrar: 0 yeni kayıt. Makbuz/audit doğrulandı; mevcut/silinmiş ödeme yeniden yazılmadı."); return;
            }
            Require(!await db.Set<CollectionMigrationBatch>().AnyAsync() && !await db.Set<CollectionContract>().AnyAsync(), "Beklenmeyen Tahsilat verisi; ilk import durduruldu.");
            foreach (var (file, sql) in ReferenceQueries) Require(await Fingerprint(db, sql) == HashFile(Path.Combine(p.Capture, file)), "İlk import hedef referansı değişmiş.");
            var batch = new CollectionMigrationBatch { SourceSystem = "MGS", SnapshotKey = snapshot, ManifestHash = Convert.FromHexString(p.CaptureHash), RuleVersion = Rule,
                NormalizationVersion = "subscriber-v1", CreatedUser = 0, CreatedDate = DateTimeOffset.UtcNow, Status = CollectionMigrationBatchStatus.Applying };
            db.Add(batch); await db.SaveChangesAsync(); var batchId = batch.Id; db.ChangeTracker.Clear();
            await ApplyReferencesAsync(db, p, batchId);
            await ApplyContractsAsync(db, p, batchId);
            await ApplyPaymentsAsync(db, p, batchId, mode == "rollback-check");
            await ApplyRelationsAsync(db, p, batchId);
            var verification = await VerifyAsync(db, p, batchId, false);
            batch = await db.Set<CollectionMigrationBatch>().SingleAsync(x => x.Id == batchId); batch.Status = CollectionMigrationBatchStatus.Reconciled; batch.CompletedDate = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(); await tx.CommitAsync();
            await Save(root, "result.json", new { Mode = "COMMITTED_REHEARSAL_IMPORT", BatchId = batchId, Verification = verification, Before = before, After = await StateAsync(db), ProductionWrites = false, OutboundCalls = 0 });
            Console.WriteLine("Prova aktarımı commit edildi; kaynak/adet/tutar/makbuz mutabakatı geçti.");
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(); db.ChangeTracker.Clear(); var after = await StateAsync(db);
            if (mode == "rollback-check" && ex is SimulatedInterruption)
            {
                Require(JsonSerializer.Serialize(before) == JsonSerializer.Serialize(after), "Rollback sonrası veri parmak izi/sayımları farklı.");
                await Save(root, "result.json", new { Mode = "SIMULATED_INTERRUPTION_ROLLED_BACK", Before = before, After = after, IdentityGapsPossible = true, ProductionWrites = false });
                Console.WriteLine("Yarım işlem provası: ilk ödeme chunk'ından sonra kesildi; tüm referans/sözleşme/ödeme/audit transactionı geri alındı."); return;
            }
            await Save(root, "failure.json", new { Exception = ex.GetType().Name, Message = ex.Message, RolledBack = true, Before = before, After = after }); throw;
        }
    }
}
