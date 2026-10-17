using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Business.Services.Crm.Collections.Calculation;
using Data.Concrete.EfCore.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Model.Concrete.Collections;
using static CollectionTransferCore;

internal static class ProductionTransfer
{
    private const string Version = "production-receipt-v1";
    private const string Live = "AssistFlow";
    private const string Rehearsal = "AssistFlow_Performance_Restore";
    private static Dictionary<string, string> CodeHashes() => new()
    {
        ["Adapter"] = HashFile(typeof(ProductionTransfer).Assembly.Location),
        ["EngineSource"] = HashFile("tools/Collections.Transfer/CollectionTransferCore.cs"),
        ["Business"] = HashFile(typeof(CollectionPaymentCommandRules).Assembly.Location),
        ["Data"] = HashFile(typeof(AppDataContext).Assembly.Location),
        ["Model"] = HashFile(typeof(CollectionMigrationBatch).Assembly.Location),
        ["Core"] = HashFile(typeof(Core.Settings.Concrete.CollectionReadOptions).Assembly.Location)
    };
    private static bool SameState(JsonElement a, JsonElement b) => a.EnumerateObject().Select(x => (x.Name, x.Value.GetString())).OrderBy(x => x.Name)
        .SequenceEqual(b.EnumerateObject().Select(x => (x.Name, x.Value.GetString())).OrderBy(x => x.Name));
    private static void VerifyBundle(string root, string expected)
    {
        Require(HashFile(Path.Combine(root, "bundle-hashes.json")) == expected, "Canlı inceleme paketi hash'i farklı.");
        foreach (var row in Doc(Path.Combine(root, "bundle-hashes.json")).EnumerateArray())
        {
            var file = Path.GetFullPath(Path.Combine(root, T(row, "File")));
            Require(file.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && HashFile(file) == T(row, "Sha256"), "Paket dosyası/yolu/hash'i farklı.");
        }
        var manifest = Doc(Path.Combine(root, "manifest.json"));
        Require(T(manifest, "TargetDatabase") == Live && T(manifest, "SourceDatabase") == "MGS" && manifest.GetProperty("RehearsalProofVerified").GetBoolean()
            && manifest.GetProperty("SchemaAndSeedsMatchRehearsal").GetBoolean(), "Canlı kaynak/şema/prova kanıtı eksik.");
    }
    private static Input Load(string[] paths)
    {
        var p = new Input { Capture = Private(paths[0]), Review = Private(paths[1]), Payments = Private(paths[2]), Relations = Private(paths[3]) };
        foreach (var path in paths.Distinct()) VerifyPackage(Private(path));
        foreach (var c in p.Contracts)
            Require(N(c, "TargetCustomerId") > 0 && c.GetProperty("Issues").GetArrayLength() == 0 && c.GetProperty("Rates").GetArrayLength() > 0
                && c.GetProperty("Gates").EnumerateArray().All(x => x.GetString() is "SERVICE_INSERT_REQUIRES_APPROVAL" or "CUSTOMER_TYPE_UPDATE_REQUIRES_APPROVAL"), "Hatalı/belirsiz sözleşme adayı aktarılmaz.");
        return p;
    }
    private static void VerifyBinding(string bundle, Input p, bool rehearsal)
    {
        var captured = Doc(Path.Combine(p.Capture, "capture-manifest.json"));
        Require(T(captured, "TargetDatabase") == (rehearsal ? Rehearsal : Live) && captured.GetProperty("RehearsalOnly").GetBoolean() == rehearsal
            && captured.GetProperty("AllObservedHashesStable").GetBoolean(), "Hedef bağlama kesiti farklı/belirsiz.");
        foreach (var file in captured.GetProperty("Files").EnumerateArray())
        {
            var name = T(file, "File"); Require(Path.GetFileName(name) == name, "Kaynak yolu geçersiz.");
            if (!name.StartsWith("live-", StringComparison.Ordinal)) Require(HashFile(Path.Combine(bundle, "source", name)) == HashFile(Path.Combine(p.Capture, name)), "Prova ve canlı kaynak satırları aynı değil; yeni bağlama gerekir.");
        }
        if (rehearsal)
        {
            var reviewHash = HashFile(Path.Combine(p.Review, "package-hashes.json"));
            Require(Path.GetFullPath(T(Doc(Path.Combine(p.Review, "summary.json")), "SourceCapturePath")) == p.Capture, "Bağlama review/capture zinciri farklı.");
            foreach (var folder in new[] { p.Payments, p.Relations })
            {
                var summary = Doc(Path.Combine(folder, "summary.json"));
                Require(T(summary, "CapturePackageHash") == p.CaptureHash && T(summary, "ReviewPackageHash") == reviewHash && !summary.GetProperty("ReadyToApply").GetBoolean(), "Bağlama ödeme/ilişki zinciri farklı.");
            }
            foreach (var name in new[] { "customers-enriched.ndjson", "groups-enriched.ndjson" })
                Require(HashFile(Path.Combine(bundle, "data", name)) == HashFile(Path.Combine(p.Relations, name)), "Cari/grup kaynak verisi prova bağlamasında değişmiş.");
        }
        // Source identifiers and financial semantics must be identical; only target reference IDs may differ.
        object ContractProjection(JsonElement c) => new { Id = N(c, "Id"), Customer = N(c, "CustomerId"), Rates = c.GetProperty("Rates").EnumerateArray().Select(r => new
            { Id = N(r, "Id"), From = T(r, "From"), To = T(r, "To"), Amount = r.GetProperty("Amount").GetDecimal(), Months = N(r, "Months"), Behavior = T(r, "Behavior"), Currency = T(r, "Currency") }).ToArray() };
        var original = Doc(Path.Combine(bundle, "data", "contract-import-plan.json")).EnumerateArray().OrderBy(x => N(x, "Id")).Select(ContractProjection);
        Require(JsonSerializer.Serialize(original) == JsonSerializer.Serialize(p.Contracts.OrderBy(x => N(x, "Id")).Select(ContractProjection)), "Prova sözleşme/tarife kapsamı canlı paketinden farklı.");
        object PaymentProjection(JsonElement r) => new { Id = N(r, "SourcePaymentId"), Hash = T(r, "SourceHash"), Contract = N(r, "SourceContractId"), Currency = T(r, "Currency"), Amount = r.GetProperty("Amount").GetDecimal(), Free = r.GetProperty("IsFree").GetBoolean() };
        Require(JsonSerializer.Serialize(Rows(Path.Combine(bundle, "data"), "payment-import-plan.ndjson").Select(PaymentProjection)) == JsonSerializer.Serialize(Rows(p.Payments, "payment-import-plan.ndjson").Select(PaymentProjection)), "Prova ödeme kapsamı canlı paketinden farklı.");
        foreach (var name in new[] { "note-plan.ndjson", "group-history-plan.ndjson", "invoice-plan.ndjson", "invoice-payment-plan.ndjson" })
        {
            string Projection(string root) => JsonSerializer.Serialize(Rows(root, name).Where(IsCandidate).Select(x => new { Id = T(x, "SourceId"), Hash = T(x, "SourceHash"), Status = T(x, "Status") }));
            Require(Projection(Path.Combine(bundle, "data")) == Projection(p.Relations), "Prova ilişkili veri kapsamı farklı: " + name);
        }
    }
    public static async Task PlanAsync(string bundlePath, string bundleHash, string[]? binding, string output)
    {
        var bundle = Private(bundlePath); VerifyBundle(bundle, bundleHash); var rehearsal = binding is not null;
        var paths = binding ?? new[] { Path.Combine(bundle, "source"), Path.Combine(bundle, "review"), Path.Combine(bundle, "data"), Path.Combine(bundle, "data") };
        var p = Load(paths); VerifyBinding(bundle, p, rehearsal);
        var schema = Doc(Path.Combine(bundle, "schema", "manifest.json"));
        var migrations = Doc(Path.Combine(bundle, "live-preflight.json")).EnumerateArray().Where(x => T(x, "Section") == "Migrations").Select(x => T(x, "MigrationId"))
            .Concat(schema.GetProperty("files").EnumerateArray().Select(x => T(x, "migrationId"))).Distinct().Order().ToArray();
        var root = NewOutput(output);
        await Save(root, "plan.json", new { Version, Bundle = bundle, BundleHash = bundleHash, Target = rehearsal ? Rehearsal : Live, Server = "sql2022", Scope = rehearsal ? "REHEARSAL" : "LIVE",
            Paths = paths.Select(Private).ToArray(), InputHashes = paths.Select(x => HashFile(Path.Combine(x, "package-hashes.json"))).ToArray(), ExpectedMigrations = migrations, CodeHashes = CodeHashes(),
            FinancialDataOnly = true, CdnUploadIncluded = false, SourceDatabaseWrites = false });
        var planHash = HashFile(Path.Combine(root, "plan.json"));
        await Save(root, "approval.template.json", new { Scope = rehearsal ? "REHEARSAL" : "LIVE", BundleHash = bundleHash, PlanHash = planHash,
            Authorization = rehearsal ? "USER_REQUESTED_REHEARSAL_ONLY" : "NOT_APPROVED", OperatorCode = "", ApprovalReference = "", FreezeAtUtc = (DateTimeOffset?)null,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(12), SharedChangesApproved = false, MaintenanceApproved = false, BackupRestoreApproved = false, StartupIsolationApproved = false,
            Evidence = Array.Empty<object>() });
        Console.WriteLine($"{(rehearsal ? "Prova" : "Canlı")} adaptör planı hazır. Plan SHA-256: {planHash}. DB bağlantısı/yazması yok; canlı onayı verilmedi.");
    }
    private static JsonElement ValidateApproval(JsonElement plan, string planHash, string approvalPath, string approvalHash, string acknowledgement)
    {
        approvalPath = Private(approvalPath); Require(HashFile(approvalPath) == approvalHash, "Onay dosyası hash'i farklı."); var a = Doc(approvalPath);
        var live = T(plan, "Scope") == "LIVE";
        Require(T(plan, "Scope") is "LIVE" or "REHEARSAL", "Bilinmeyen adaptör kapsamı.");
        Require(T(plan, "Version") == Version && T(plan, "Target") == (live ? Live : Rehearsal) && T(a, "Scope") == T(plan, "Scope")
            && T(a, "BundleHash") == T(plan, "BundleHash") && T(a, "PlanHash") == planHash && acknowledgement == (live ? "APPLY_LIVE_ASSISTFLOW" : "REHEARSAL_ONLY"), "Hedef/kapsam/teyit/onay bağı farklı.");
        Require(a.GetProperty("ExpiresAtUtc").GetDateTimeOffset() > DateTimeOffset.UtcNow, "Onay süresi dolmuş.");
        Require(live ? T(a, "Authorization") == "EXPLICIT_PRODUCTION_APPROVAL" : T(a, "Authorization") == "USER_REQUESTED_REHEARSAL_ONLY", "Canlı uygulama açıkça onaylanmadı.");
        if (live)
        {
            foreach (var field in new[] { "SharedChangesApproved", "MaintenanceApproved", "BackupRestoreApproved", "StartupIsolationApproved" }) Require(a.GetProperty(field).GetBoolean(), "Canlı geçiş onayı eksik: " + field);
            Require(T(a, "OperatorCode").Length > 0 && T(a, "ApprovalReference").Length > 0, "Gerçek operatör/onay kaydı gereklidir.");
            var freeze = a.GetProperty("FreezeAtUtc").GetDateTimeOffset(); var capture = Doc(Path.Combine(T(plan, "Bundle"), "source", "capture-manifest.json"));
            var captured = capture.GetProperty("StartedAtUtc").GetDateTimeOffset();
            Require(freeze <= captured && DateTimeOffset.UtcNow - captured < TimeSpan.FromMinutes(30), "Bakım sonrası taze son kesit gerekli; eski inceleme paketi uygulanamaz.");
            var evidence = a.GetProperty("Evidence").EnumerateArray().ToArray();
            foreach (var kind in new[] { "HumanApproval", "MaintenanceFreeze", "BackupRestore", "StartupIsolation", "SharedChanges", "AdapterRehearsalAcceptance" })
                Require(evidence.Any(x => T(x, "Kind") == kind), "Onay kanıtı eksik: " + kind);
            foreach (var e in evidence) Require(HashFile(Private(T(e, "Path"))) == T(e, "Sha256"), "Onay kanıtı hash'i farklı.");
            var acceptance = Doc(Private(T(evidence.Single(x => T(x, "Kind") == "AdapterRehearsalAcceptance"), "Path")));
            Require(acceptance.GetProperty("WritePathVerified").GetBoolean() && T(acceptance, "BundleHash") == T(plan, "BundleHash")
                && SameState(acceptance.GetProperty("CodeHashes"), plan.GetProperty("CodeHashes")), "Aynı adaptör/paket için başarılı yazmalı prova kanıtı gerekli.");
            foreach (var e in acceptance.GetProperty("Evidence").EnumerateArray()) Require(HashFile(Private(T(e, "Path"))) == T(e, "Sha256"), "Adaptör prova makbuzu değişmiş.");
        }
        return a;
    }
    private static async Task<AppDataContext> OpenAsync(string config, bool live, JsonElement plan, JsonElement approval, bool readOnly)
    {
        using var cfg = JsonDocument.Parse(await File.ReadAllTextAsync(config), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var connectionText = Environment.GetEnvironmentVariable("COLLECTION_RELEASE_CONNECTIONSTRING") ?? cfg.RootElement.GetProperty("AppSettings").GetProperty("MSSQLConnectionString").GetString();
        var builder = new SqlConnectionStringBuilder(connectionText); Require(builder.InitialCatalog == Live, "Kaynak config AssistFlow olmalıdır.");
        builder.InitialCatalog = live ? Live : Rehearsal; builder.ApplicationName = readOnly ? "CollectionProductionAdapterReadOnly" : "CollectionProductionAdapter"; builder.ConnectTimeout = 10;
        if (readOnly) builder.ApplicationIntent = ApplicationIntent.ReadOnly;
        var db = new AppDataContext(new DbContextOptionsBuilder<AppDataContext>().UseSqlServer(builder.ConnectionString, x => x.CommandTimeout(300)).Options);
        await db.Database.OpenConnectionAsync();
        // These statements only inspect; the target is a fixed enum, never user SQL.
        await db.Database.ExecuteSqlRawAsync(live
            ? "IF DB_NAME()<>N'AssistFlow' OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'sql2022' THROW 51000,N'Canlı hedef farklı.',1;"
            : "IF DB_NAME()<>N'AssistFlow_Performance_Restore' OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'sql2022' THROW 51000,N'Prova hedefi farklı.',1;");
        await db.Database.ExecuteSqlRawAsync("IF EXISTS(SELECT 1 FROM sys.triggers WHERE is_disabled=0) OR EXISTS(SELECT 1 FROM sys.server_triggers WHERE is_disabled=0) THROW 51000,N'Etkin trigger: ayrı inceleme gerekli.',1;");
        if (!readOnly) await db.Database.ExecuteSqlRawAsync("IF EXISTS(SELECT 1 FROM sys.dm_exec_sessions WHERE database_id=DB_ID() AND is_user_process=1 AND session_id<>@@SPID) THROW 51000,N'Hedefte başka kullanıcı oturumu var; zorla sonlandırılmaz, aktarım durduruldu.',1;");
        if (!live) await db.Database.ExecuteSqlRawAsync("IF EXISTS(SELECT 1 FROM sys.databases WHERE database_id=DB_ID() AND (is_trustworthy_on=1 OR is_db_chaining_on=1 OR is_broker_enabled=1)) THROW 51000,N'Prova izolasyon ayarı farklı.',1;");
        var actualMigrations = (await db.Database.GetAppliedMigrationsAsync()).Order().ToArray();
        Require(actualMigrations.SequenceEqual(plan.GetProperty("ExpectedMigrations").EnumerateArray().Select(x => x.GetString()!)) && !(await db.Database.GetPendingMigrationsAsync()).Any(), "Migration baseline'ı farklı/eksik.");
        Require(!await db.Users.AnyAsync(x => x.Id == 0), "Sistem aktörü gerçek kullanıcıyla çakışıyor.");
        if (live)
        {
            var code = T(approval, "OperatorCode"); Require(await db.Users.CountAsync(x => x.Code == code && !x.IsDeleted) == 1, "Onaylayan kullanıcı tekil/aktif değil.");
            await db.Database.ExecuteSqlRawAsync("""
                IF (SELECT COUNT(*) FROM msdb.dbo.sysjobs WHERE name IN(N'Platform Tahsilat Bakım',N'Platform SIMStoMGS-MANITOUTOMGS',N'Manitou2Flowassist',N'Platform GrupTopluMail'))<>4
                  OR EXISTS(SELECT 1 FROM msdb.dbo.sysjobs WHERE name IN(N'Platform Tahsilat Bakım',N'Platform SIMStoMGS-MANITOUTOMGS',N'Manitou2Flowassist',N'Platform GrupTopluMail') AND enabled=1)
                  OR EXISTS(SELECT 1 FROM msdb.dbo.sysjobactivity a JOIN msdb.dbo.sysjobs j ON j.job_id=a.job_id WHERE a.session_id=(SELECT MAX(session_id) FROM msdb.dbo.syssessions) AND a.start_execution_date IS NOT NULL AND a.stop_execution_date IS NULL AND j.name IN(N'Platform Tahsilat Bakım',N'Platform SIMStoMGS-MANITOUTOMGS',N'Manitou2Flowassist',N'Platform GrupTopluMail'))
                  THROW 51000,N'Kaynak/hedef Job durdurma ve tamamlanma koşulu sağlanmadı.',1;
                """);
        }
        return db;
    }
    public static async Task RunAsync(string command, string config, string planRoot, string planHash, string approvalPath, string approvalHash, string output, string acknowledgement)
    {
        planRoot = Private(planRoot); Require(HashFile(Path.Combine(planRoot, "plan.json")) == planHash, "Adaptör plan hash'i farklı."); var plan = Doc(Path.Combine(planRoot, "plan.json"));
        var approval = ValidateApproval(plan, planHash, approvalPath, approvalHash, acknowledgement); var live = T(plan, "Scope") == "LIVE";
        Require(!live || command == "apply", "Canlıda hata/silme/düzenleme deneyi çalıştırılamaz.");
        Require(SameState(JsonSerializer.SerializeToElement(CodeHashes()), plan.GetProperty("CodeHashes")), "Aktarıcı/iş kuralı derlemesi değişmiş; plan yeniden doğrulanmalı.");
        var bundle = Private(T(plan, "Bundle")); VerifyBundle(bundle, T(plan, "BundleHash"));
        var paths = plan.GetProperty("Paths").EnumerateArray().Select(x => x.GetString()!).ToArray(); var p = Load(paths); VerifyBinding(bundle, p, !live);
        Require(paths.Select(x => HashFile(Path.Combine(x, "package-hashes.json"))).SequenceEqual(plan.GetProperty("InputHashes").EnumerateArray().Select(x => x.GetString()!)), "Kaynak bağlama hash'i değişmiş.");
        var root = NewOutput(output);
        if (live)
        {
            await LiveTransferPreview.RunAsync(config, Path.Combine(root, "source-recheck"));
            var recheck = Doc(Path.Combine(root, "source-recheck", "capture-manifest.json"));
            Require(recheck.GetProperty("AllObservedHashesStable").GetBoolean(), "Son kaynak okuması sabit değil.");
            // A replay legitimately has newly inserted services/parents and changed customer types.
            // Check all source bytes here; initial target fingerprints are checked inside the locked first-import transaction.
            foreach (var f in recheck.GetProperty("Files").EnumerateArray().Where(x => !T(x, "File").StartsWith("live-", StringComparison.Ordinal)))
                Require(HashFile(Path.Combine(p.Capture, T(f, "File"))) == HashFile(Path.Combine(root, "source-recheck", T(f, "File"))), "Son kaynak verisi değişmiş; uygulama yapılmadı.");
            // Dispose the idle read-only preview pools owned by THIS standalone process.
            // Otherwise our own preview session would trip the no-other-user-session guard.
            // This never terminates sessions belonging to the user's app or other processes.
            SqlConnection.ClearAllPools();
        }
        await using var db = await OpenAsync(config, live, plan, approval, command == "verify-existing");
        var snapshot = Version + ":" + T(plan, "BundleHash") + ":" + T(plan, "Scope");
        if (command == "verify-existing")
        {
            // Read-only verification against the previous rehearsal's batch; no adoption or rewrite.
            var oldSnapshot = "rehearsal-receipt-v1-" + p.CaptureHash;
            var batch = await db.Set<CollectionMigrationBatch>().AsNoTracking().SingleAsync(x => x.SourceSystem == "MGS" && x.SnapshotKey == oldSnapshot && x.Status == CollectionMigrationBatchStatus.Reconciled);
            var before = await StateAsync(db); var verification = await VerifyAsync(db, p, batch.Id, true); var after = await StateAsync(db);
            Require(JsonSerializer.Serialize(before) == JsonSerializer.Serialize(after), "Okuma sırasında veri değişti; doğrulama tutarlı değil.");
            await Save(root, "result.json", new { Mode = "READ_ONLY_EXISTING_REHEARSAL_VERIFICATION", BatchId = batch.Id, Verification = verification, Before = before, After = after,
                BundleHash = T(plan, "BundleHash"), PlanHash = planHash, DatabaseWrites = false, WritePathVerified = false });
            Console.WriteLine("Mevcut prova verisi salt-okunur doğrulandı. Yeni adaptörün ilk yazma/rollback provası yapılmış sayılmaz."); return;
        }
        if (command == "resilience-check")
        {
            await ResilienceAsync(db, p, root, snapshot);
            await Save(root, "binding.json", new { Version, PlanHash = planHash, BundleHash = T(plan, "BundleHash"), CodeHashes = CodeHashes(), Target = Rehearsal }); return;
        }
        await Save(root, "started.json", new { Target = T(plan, "Target"), BundleHash = T(plan, "BundleHash"), PlanHash = planHash, ApprovalHash = approvalHash, StartedAtUtc = DateTimeOffset.UtcNow });
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        Dictionary<string, string>? beforeState = null; object? verified = null; long id = 0; bool replayed = false; bool commitAttempted = false;
        try
        {
            await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'CollectionProductionApply.v1',@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=15000; IF @r<0 THROW 51000,N'Aktarım kilidi alınamadı.',1;");
            beforeState = await StateAsync(db);
            var existing = await db.Set<CollectionMigrationBatch>().AsNoTracking().SingleOrDefaultAsync(x => x.SourceSystem == "MGS" && x.SnapshotKey == snapshot);
            if (existing is not null)
            {
                Require(command != "rollback-check" && existing.Status == CollectionMigrationBatchStatus.Reconciled && existing.ManifestHash.SequenceEqual(Convert.FromHexString(T(plan, "BundleHash"))) && existing.RuleVersion == Version, "Yarım/farklı aktarım otomatik sürdürülmez.");
                var proof = await db.Set<CollectionMigrationSourceRow>().AsNoTracking().SingleAsync(x => x.BatchId == existing.Id && x.EntityCode == "ExecutionApproval" && x.SourceId == "plan");
                using var proofJson = JsonDocument.Parse(proof.Payload);
                Require(proof.PayloadHash.SequenceEqual(Hash(proof.Payload)) && T(proofJson.RootElement, "PlanHash") == planHash, "Önceki aktarımın plan/onay audit'i farklı; otomatik tekrar durduruldu.");
                id = existing.Id; verified = await VerifyAsync(db, p, id, true); replayed = true; await tx.RollbackAsync();
            }
            else
            {
                Require(beforeState.Where(x => x.Key.StartsWith("collection.", StringComparison.Ordinal)).All(x => x.Value == Convert.ToHexString(SHA256.HashData(Array.Empty<byte>()))), "İlk canlı/prova aktarımı boş collection veri baseline'ı gerektirir; mevcut kayıt silinmez.");
                foreach (var (file, sql) in ReferenceQueries) Require(await Fingerprint(db, sql) == HashFile(Path.Combine(p.Capture, file)), "Müşteri/tanım baseline'ı değişmiş: " + file);
                var batch = new CollectionMigrationBatch { SourceSystem = "MGS", SnapshotKey = snapshot, ManifestHash = Convert.FromHexString(T(plan, "BundleHash")), RuleVersion = Version,
                    NormalizationVersion = "subscriber-v1", CreatedUser = 0, CreatedDate = DateTimeOffset.UtcNow, Status = CollectionMigrationBatchStatus.Applying };
                db.Add(batch); await db.SaveChangesAsync(); id = batch.Id; db.ChangeTracker.Clear();
                await AuditAsync(db, id, "ExecutionApproval", new[] { ("plan", (string?)null, JsonSerializer.SerializeToElement(new { Plan = plan, Approval = approval, PlanHash = planHash, ApprovalHash = approvalHash })) });
                await ApplyReferencesAsync(db, p, id); await ApplyContractsAsync(db, p, id); await ApplyPaymentsAsync(db, p, id, command == "rollback-check"); await ApplyRelationsAsync(db, p, id);
                verified = await VerifyAsync(db, p, id, false); batch = await db.Set<CollectionMigrationBatch>().SingleAsync(x => x.Id == id); batch.Status = CollectionMigrationBatchStatus.Reconciled; batch.CompletedDate = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(); commitAttempted = true; await tx.CommitAsync();
            }
        }
        catch (Exception ex)
        {
            if (commitAttempted)
            {
                await Save(root, "commit-uncertain.json", new { Target = T(plan, "Target"), BatchId = id, BundleHash = T(plan, "BundleHash"), PlanHash = planHash, Status = "RECONCILE_BEFORE_RETRY", Exception = ex.GetType().Name }); throw;
            }
            await tx.RollbackAsync(); db.ChangeTracker.Clear(); var afterRollback = await StateAsync(db);
            var equal = beforeState is not null && JsonSerializer.Serialize(beforeState) == JsonSerializer.Serialize(afterRollback);
            await Save(root, "rollback.json", new { Target = T(plan, "Target"), PlanHash = planHash, BundleHash = T(plan, "BundleHash"), ControlledInterruption = ex is SimulatedInterruption, StateUnchanged = equal, Before = beforeState, After = afterRollback, Exception = ex.GetType().Name });
            if (command == "rollback-check" && ex is SimulatedInterruption) { Require(equal, "Rollback veri durumu farklı."); Console.WriteLine("Adaptör kesinti/rollback provası başarılı."); return; }
            throw;
        }
        // After commit never claim rollback. A failed local read/file write is not a failed DB commit.
        await Save(root, "commit.json", new { Target = T(plan, "Target"), BatchId = id, BundleHash = T(plan, "BundleHash"), PlanHash = planHash, ApprovalHash = approvalHash, Replayed = replayed, Committed = !replayed });
        var afterState = await StateAsync(db);
        if (replayed) Require(JsonSerializer.Serialize(beforeState) == JsonSerializer.Serialize(afterState), "Tekrar çalışmasında veri değişmiş.");
        await Save(root, "result.json", new { Mode = replayed ? "IDEMPOTENT_REPLAY" : "COMMITTED_IMPORT", Target = T(plan, "Target"), BatchId = id, Verification = verified, Before = beforeState, After = afterState,
            PlanHash = planHash, BundleHash = T(plan, "BundleHash"), SourceDatabaseWrites = false, OutboundCalls = 0 });
        Console.WriteLine(replayed ? "Adaptör tekrarı: 0 yeni kayıt; mevcut veriler korunuyor." : "Adaptör aktarımı commit edildi; makbuz/audit/tutar mutabakatı başarılı.");
    }
    public static async Task CertifyAsync(string planRoot, string expectedPlanHash, string[] evidenceRoots, string output)
    {
        planRoot = Private(planRoot); Require(HashFile(Path.Combine(planRoot, "plan.json")) == expectedPlanHash, "Plan hash'i farklı.");
        var plan = Doc(Path.Combine(planRoot, "plan.json")); Require(T(plan, "Scope") == "REHEARSAL" && T(plan, "Target") == Rehearsal
            && SameState(plan.GetProperty("CodeHashes"), JsonSerializer.SerializeToElement(CodeHashes())), "Aynı adaptör derlemesinin prova planı gereklidir.");
        var files = new[] { Path.Combine(Private(evidenceRoots[0]), "result.json"), Path.Combine(Private(evidenceRoots[1]), "result.json"),
            Path.Combine(Private(evidenceRoots[2]), "rollback.json"), Path.Combine(Private(evidenceRoots[3]), "result.json"), Path.Combine(Private(evidenceRoots[3]), "binding.json") };
        var applied = Doc(files[0]); var replay = Doc(files[1]); var rollback = Doc(files[2]); var resilience = Doc(files[3]); var binding = Doc(files[4]);
        foreach (var r in new[] { applied, replay, rollback, binding }) Require(T(r, "Target") == Rehearsal && T(r, "PlanHash") == expectedPlanHash && T(r, "BundleHash") == T(plan, "BundleHash"), "Prova kanıtı başka hedef/plan/pakete ait.");
        Require(T(applied, "Mode") == "COMMITTED_IMPORT" && T(replay, "Mode") == "IDEMPOTENT_REPLAY" && rollback.GetProperty("ControlledInterruption").GetBoolean()
            && rollback.GetProperty("StateUnchanged").GetBoolean() && SameState(rollback.GetProperty("Before"), rollback.GetProperty("After"))
            && SameState(applied.GetProperty("After"), replay.GetProperty("After")) && SameState(replay.GetProperty("Before"), replay.GetProperty("After"))
            && SameState(applied.GetProperty("After"), resilience.GetProperty("After")) && SameState(resilience.GetProperty("Before"), resilience.GetProperty("After"))
            && resilience.GetProperty("Checks").GetArrayLength() == 4 && resilience.GetProperty("Checks").EnumerateArray().All(x => x.GetProperty("Passed").GetBoolean() && x.GetProperty("RolledBack").GetBoolean()), "İlk yazma/rollback/tekrar/koruma kabulü tamamlanmamış.");
        var root = NewOutput(output); await Save(root, "acceptance.json", new { Version, BundleHash = T(plan, "BundleHash"), PlanHash = expectedPlanHash, CodeHashes = CodeHashes(), WritePathVerified = true,
            Evidence = files.Select(x => new { Path = x, Sha256 = HashFile(x) }).ToArray(), ProductionAuthorizationGranted = false });
        Console.WriteLine("Aynı adaptör/paket için yazmalı prova makbuzu hazır; bu insan/canlı uygulama onayı değildir.");
    }
}
