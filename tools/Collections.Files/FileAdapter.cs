using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Amazon.Runtime;
using Amazon.S3;
using Core.Settings.Concrete;
using Data.Concrete.EfCore.Collections;
using Data.Concrete.EfCore.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Model.Concrete.Collections;

internal sealed class FileGateException(string message) : Exception(message);
internal sealed record FileCandidate(string Kind, string SourceId, long LegacyOwner, long RetainedOwner, long CustomerId,
    long? ContractId, string Subscriber, string ContractVersion, long EvidenceId, string EvidenceHash, string SourcePath,
    string OriginalName, long Size, string ContentHash, string ContentType, string StoredName);
internal sealed record FilePlan(string Version, string Scope, string Bundle, string BundleHash, string Root, string ActivityArchive,
    string ActivityHash, long BatchId, string BatchVersion, string FinancialAcceptance, string FinancialAcceptanceHash,
    string CodeHash, string NamespaceHash, string SourceProjectionHash, DateTimeOffset PlannedAtUtc, FileCandidate[] Files);
internal sealed record AttachResult(long AttachmentId, bool Created, bool ExistingObject);

internal static class FileAdapter
{
    internal const string Version = "collection-cdn-adapter-v1";
    internal static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    internal static void Need(bool condition, string message) { if (!condition) throw new FileGateException(message); }
    internal static string HashText(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    internal static string HashFile(string path) { using var s = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(s)); }
    internal static JsonElement Doc(string path) { using var doc = JsonDocument.Parse(File.ReadAllText(path)); return doc.RootElement.Clone(); }
    internal static string Text(JsonElement r, string key) => r.TryGetProperty(key, out var p) && p.ValueKind != JsonValueKind.Null ? p.ToString().Trim() : "";
    internal static long Num(JsonElement r, string key) => long.TryParse(Text(r, key), out var n) ? n : 0;
    internal static string Private(string path)
    {
        var full = Path.GetFullPath(path); var root = Path.GetFullPath(".tools") + Path.DirectorySeparatorChar;
        Need(full.StartsWith(root, StringComparison.OrdinalIgnoreCase), "Kanıt yalnız özel .tools dizininde olabilir."); SafeNoLink(full); return full;
    }
    internal static void SafeNoLink(string full)
    {
        for (var p = full; !string.IsNullOrEmpty(p); p = Path.GetDirectoryName(p))
            if (File.Exists(p) || Directory.Exists(p)) Need((File.GetAttributes(p) & FileAttributes.ReparsePoint) == 0, "Link/reparse yolu reddedildi.");
    }
    internal static string NewOutput(string path) { var full = Private(path); Need(!File.Exists(full) && !Directory.Exists(full), "Mevcut çıktı üzerine yazılmaz."); Directory.CreateDirectory(full); return full; }
    internal static Task Save(string path, object value) => File.WriteAllTextAsync(path, JsonSerializer.Serialize(value, Json));
    internal static string CodeHash() => HashText(string.Join("|", new[] { typeof(FileAdapter).Assembly.Location,
        typeof(Business.Services.Storage.R2FileStorage).Assembly.Location, typeof(AppDataContext).Assembly.Location,
        typeof(CollectionContractAttachment).Assembly.Location, typeof(R2StorageOptions).Assembly.Location, typeof(AmazonS3Client).Assembly.Location }.Select(HashFile)));
    internal static string NamespaceHash(R2StorageOptions o) => HashText(JsonSerializer.Serialize(new { o.Endpoint, o.BucketName, o.KeyPrefix, o.PublicBaseUrl }));
    internal static JsonElement Configuration(string path)
    { using var doc = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }); return doc.RootElement.Clone(); }
    internal static R2StorageOptions LiveOptions(string config)
    {
        var options = Configuration(config).GetProperty("R2Storage").Deserialize<R2StorageOptions>()!;
        Need(Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpoint) && endpoint.Scheme == "https" && endpoint.Host.EndsWith(".r2.cloudflarestorage.com", StringComparison.OrdinalIgnoreCase)
            && endpoint.UserInfo.Length == 0 && endpoint.Query.Length == 0, "Production R2 endpoint doğrulanamadı.");
        Need(Uri.TryCreate(options.PublicBaseUrl, UriKind.Absolute, out var url) && url.Scheme == "https" && url.Host == "cdn.flowassist.mgs.com.tr" && url.Query.Length == 0 && url.UserInfo.Length == 0,
            "Production public host doğrulanamadı.");
        Need(!string.IsNullOrWhiteSpace(options.BucketName) && !string.IsNullOrWhiteSpace(options.AccessKeyId) && !string.IsNullOrWhiteSpace(options.SecretAccessKey)
            && options.KeyPrefix == options.KeyPrefix.Trim('/') && options.KeyPrefix.Length > 0 && !options.KeyPrefix.Contains("test", StringComparison.OrdinalIgnoreCase)
            && !options.KeyPrefix.Contains("rehearsal", StringComparison.OrdinalIgnoreCase) && !options.KeyPrefix.Split('/').Any(x => x is ".." or "." or ""), "Production namespace/credential doğrulanamadı."); return options;
    }
    internal static async Task<AppDataContext> OpenAsync(string config, string scope)
    {
        Need(scope is "LIVE" or "REHEARSAL", "Bilinmeyen hedef kapsamı.");
        var c = Configuration(config); var b = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("COLLECTION_RELEASE_CONNECTIONSTRING") ?? c.GetProperty("AppSettings").GetProperty("MSSQLConnectionString").GetString());
        Need(b.InitialCatalog == "AssistFlow" && b.DataSource is "192.168.1.8" or "sql2022", "Kaynak bağlantı hedefi farklı.");
        b.InitialCatalog = scope == "LIVE" ? "AssistFlow" : "AssistFlow_Performance_Restore";
        b.ApplicationName = "CollectionFiles" + scope; b.ConnectTimeout = 10;
        var db = new AppDataContext(new DbContextOptionsBuilder<AppDataContext>().UseSqlServer(b.ConnectionString, x => x.CommandTimeout(120)).Options);
        try
        {
            await db.Database.OpenConnectionAsync();
            var guard = scope == "LIVE" ? "IF DB_NAME()<>N'AssistFlow' THROW 51000,N'Yanlış canlı hedef.',1;" : "IF DB_NAME()<>N'AssistFlow_Performance_Restore' THROW 51000,N'Yanlış prova hedef.',1;";
            await db.Database.ExecuteSqlRawAsync(guard + " IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'sql2022' THROW 51000,N'Yanlış sunucu.',1;");
            await db.Database.ExecuteSqlRawAsync("IF EXISTS(SELECT 1 FROM sys.triggers WHERE is_disabled=0) OR EXISTS(SELECT 1 FROM sys.server_triggers WHERE is_disabled=0) THROW 51000,N'Etkin trigger; ayrı inceleme gerekli.',1;");
            if (scope == "REHEARSAL") await db.Database.ExecuteSqlRawAsync("IF EXISTS(SELECT 1 FROM sys.databases WHERE database_id=DB_ID() AND (is_trustworthy_on=1 OR is_db_chaining_on=1 OR is_broker_enabled=1)) THROW 51000,N'Prova izolasyonu farklı.',1;");
            Need(!(await db.Database.GetPendingMigrationsAsync()).Any(), "Eksik migration var; otomatik kurulum yapılmaz.");
            return db;
        }
        catch { await db.DisposeAsync(); throw; }
    }
    internal static async Task LockAsync(AppDataContext db)
    {
        await db.Database.ExecuteSqlRawAsync("IF EXISTS(SELECT 1 FROM sys.dm_exec_sessions WHERE database_id=DB_ID() AND is_user_process=1 AND session_id<>@@SPID) THROW 51000,N'Hedefte başka oturum var; KILL uygulanmaz.',1; DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'CollectionCdnFileTransfer',@LockMode=N'Exclusive',@LockOwner=N'Session',@LockTimeout=0; IF @r<0 THROW 51000,N'Dosya aktarım kilidi alınamadı.',1;");
    }
    internal static void VerifyBundle(string root, string hash)
    {
        Need(HashFile(Path.Combine(root, "bundle-hashes.json")) == hash, "Bundle hash farklı.");
        foreach (var entry in Doc(Path.Combine(root, "bundle-hashes.json")).EnumerateArray())
        {
            var full = Path.GetFullPath(Path.Combine(root, Text(entry, "File")));
            Need(full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "Bundle yolu güvensiz."); SafeNoLink(full);
            Need(HashFile(full) == Text(entry, "Sha256"), "Bundle içeriği değişmiş.");
        }
        Need(Text(Doc(Path.Combine(root, "manifest.json")), "TargetDatabase") == "AssistFlow", "Canlı inceleme paketi gerekli.");
    }
    internal static async Task PlanAsync(string command, string config, string bundlePath, string bundleHash, string sourceRoot, string archive, long batchId, string financialAcceptance, string output)
    {
        var scope = command == "plan-live" ? "LIVE" : "REHEARSAL"; var bundle = Private(bundlePath); VerifyBundle(bundle, bundleHash);
        var acceptancePath = Private(financialAcceptance); var acceptance = Doc(acceptancePath);
        Need(acceptance.GetProperty("WritePathVerified").GetBoolean() && Text(acceptance, "BundleHash") == bundleHash, "Aynı bundle için finansal prova kabulü gerekli.");
        var root = Path.GetFullPath(sourceRoot); SafeNoLink(root); Need(Directory.Exists(root) && !root.StartsWith("\\", StringComparison.Ordinal), "Yerel arşiv kökü gerekli.");
        archive = Path.GetFullPath(archive); SafeNoLink(archive); Need(File.Exists(archive), "Aktivite arşivi bulunamadı.");
        await using var db = await OpenAsync(config, scope);
        var batch = await db.Set<CollectionMigrationBatch>().AsNoTracking().SingleAsync(x => x.Id == batchId);
        Need(batch.SourceSystem == "MGS" && batch.Status == CollectionMigrationBatchStatus.Reconciled && Convert.ToHexString(batch.ManifestHash) == bundleHash, "Finansal batch/bundle farklı veya tamamlanmamış.");
        var financial = await db.Set<CollectionMigrationSourceRow>().AsNoTracking().Where(x => x.BatchId == batchId && x.EntityCode == "ExecutionApproval").SingleAsync();
        Need(HashText(financial.Payload) == Convert.ToHexString(financial.PayloadHash), "Finansal onay audit'i farklı.");
        var contractAudit = await db.Set<CollectionMigrationSourceRow>().AsNoTracking().Where(x => x.BatchId == batchId && x.EntityCode == "ContractFileEvidence").ToDictionaryAsync(x => x.SourceId);
        var activityAudit = await db.Set<CollectionMigrationSourceRow>().AsNoTracking().Where(x => x.BatchId == batchId && x.EntityCode == "CustomerFileEvidence").ToDictionaryAsync(x => x.SourceId);
        var maps = await db.Set<CollectionMigrationMap>().AsNoTracking().Where(x => x.FirstBatchId == batchId && x.EntityCode == "Contract" && x.SourceSystem == "MGS").ToDictionaryAsync(x => x.SourceId);
        var contracts = await db.Set<CollectionContract>().AsNoTracking().ToDictionaryAsync(x => x.Id);
        var customers = await CollectionCustomerScopeQuery.Customers(db.Customers.AsNoTracking()).ToListAsync();
        var sourceCustomers = File.ReadLines(Path.Combine(bundle, "source", "customers.ndjson")).Select(x => JsonSerializer.Deserialize<JsonElement>(x)).ToDictionary(x => Num(x, "CustomerID"));
        var options = scope == "LIVE" ? LiveOptions(config) : FileTransport.IsolatedOptions(); var ns = NamespaceHash(options);
        var files = new List<FileCandidate>();
        void Add(string kind, JsonElement row, string sourceId, long owner, long retained, long customer, long? contract, string version, CollectionMigrationSourceRow evidence, string path, string name, long size, string contentHash)
        {
            Need(HashText(evidence.Payload) == Convert.ToHexString(evidence.PayloadHash), "Dosya kanıtı hash farklı.");
            Need(sourceCustomers.TryGetValue(retained, out var source), "Korunan kaynak müşteri yok."); var subscriber = Text(source, "SubscriberNo");
            Need(subscriber.Length > 0 && customers.Count(x => x.SubscriberCode?.Trim() == subscriber) == 1 && customers.Single(x => x.SubscriberCode?.Trim() == subscriber).Id == customer, "Abone/owner tekil eşleşmiyor.");
            var type = ContentType(name); Need(type.Length > 0 && name.Length <= 260 && Path.GetFileName(name) == name && size is > 0 and <= 20971520 && contentHash.Length == 64, "Dosya metadata geçersiz.");
            var receiptSource = kind == "CONTRACT" ? sourceId : HashText(path);
            var key = "collection-live-" + HashText(Version + "|" + scope + "|" + ns + "|" + kind + "|" + receiptSource + "|" + customer + "|" + contract + "|" + contentHash).ToLowerInvariant() + Path.GetExtension(name).ToLowerInvariant();
            files.Add(new(kind, receiptSource, owner, retained, customer, contract, subscriber, version, evidence.Id, Convert.ToHexString(evidence.PayloadHash), path, name, size, contentHash, type, key));
        }
        foreach (var line in File.ReadLines(Path.Combine(bundle, "data", "contract-file-plan.ndjson")))
        {
            var row = JsonSerializer.Deserialize<JsonElement>(line); if (Text(row, "Status") != "LOCAL_VERIFIED_PENDING_PRODUCTION_CDN") continue;
            var id = Text(row, "SourceContractId"); Need(contractAudit.ContainsKey(id) && maps.TryGetValue(id, out var map) && map.TargetContractId.HasValue, "Sözleşme dosya audit/map eksik.");
            var target = contracts[maps[id].TargetContractId!.Value]; Need(!target.IsDeleted, "Hedef sözleşme silinmiş.");
            var proof = JsonSerializer.Deserialize<JsonElement>(contractAudit[id].Payload); var original = proof.GetProperty("Plan");
            Need(Num(proof, "TargetContractId") == target.Id && Text(original, "SourceHash") == Text(row, "SourceHash") && Text(original, "RelativePath") == Text(row, "RelativePath")
                && Text(original, "OriginalName") == Text(row, "OriginalName") && Text(original.GetProperty("File"), "SHA256") == Text(row.GetProperty("File"), "SHA256"), "Sözleşme dosya kanıtı değişmiş.");
            Add("CONTRACT", row, id, Num(row, "SourceCustomerId"), Num(row, "SourceCustomerId"), target.CustomerId, target.Id, Convert.ToHexString(target.RowVersion), contractAudit[id], Text(row, "RelativePath"), Text(row, "OriginalName"), Num(row.GetProperty("File"), "Size"), Text(row.GetProperty("File"), "SHA256"));
        }
        foreach (var row in Doc(Path.Combine(bundle, "data", "activity-file-plan.json")).EnumerateArray().Where(x => Text(x, "Status") == "LOCAL_VERIFIED_PENDING_PRODUCTION_CDN"))
        {
            var path = Text(row, "Path"); var sourceKey = HashText(path); Need(activityAudit.ContainsKey(sourceKey), "Müşteri dosyası audit'i yok.");
            var proof = JsonSerializer.Deserialize<JsonElement>(activityAudit[sourceKey].Payload);
            Need(Text(proof, "SHA256") == Text(row, "SHA256") && Text(proof, "RetainedLegacyCustomerId") == Text(row, "RetainedLegacyCustomerId") && Text(proof, "Path") == path, "Müşteri dosya sahiplik kanıtı farklı.");
            var winner = Num(row, "RetainedLegacyCustomerId"); var subscriber = Text(sourceCustomers[winner], "SubscriberNo"); var customer = customers.Single(x => x.SubscriberCode?.Trim() == subscriber).Id;
            Add("CUSTOMER", row, sourceKey, Num(row, "SourceCustomerId"), winner, customer, null, "", activityAudit[sourceKey], path, path.Replace('\\', '/').Split('/').Last(), Num(row, "Size"), Text(row, "SHA256"));
        }
        Need(files.Count > 0 && files.Select(x => x.Kind + x.SourceId).Distinct().Count() == files.Count, "Boş veya tekrarlı dosya planı.");
        foreach (var file in files) { var bytes = await BytesAsync(root, archive, file); ValidateBytes(file, bytes); }
        var projection = HashText(JsonSerializer.Serialize(files.Select(x => new { x.Kind, x.SourceId, x.LegacyOwner, x.RetainedOwner, x.SourcePath, x.ContentHash, x.Size, x.ContentType, x.OriginalName })));
        var plan = new FilePlan(Version, scope, bundle, bundleHash, root, archive, HashFile(archive), batchId, Convert.ToHexString(batch.RowVersion), acceptancePath, HashFile(acceptancePath), CodeHash(), ns, projection, DateTimeOffset.UtcNow, files.ToArray());
        var dir = NewOutput(output); await Save(Path.Combine(dir, "plan.json"), plan); var hash = HashFile(Path.Combine(dir, "plan.json"));
        await Save(Path.Combine(dir, "approval.template.json"), new { Authorization = "NOT_APPROVED", PlanHash = hash, Scope = scope, OperatorCode = "", ApprovalReference = "", ExpiresAtUtc = (DateTimeOffset?)null,
            MaintenanceApproved = false, BackupApproved = false, ScanApproved = false, PublicExposureAccepted = false, NamespaceApproved = false, FinancialReplayApproved = false,
            MaxFiles = 0, Mode = "PILOT_OR_FULL", RehearsalAcceptancePath = "", RehearsalAcceptanceHash = "", Evidence = Array.Empty<object>() });
        Console.WriteLine($"{scope} dosya planı: {files.Count} ilişki. SHA-256: {hash}. DB/CDN yazması yapılmadı.");
    }
    internal static string ContentType(string name) => Path.GetExtension(name).ToLowerInvariant() switch { ".pdf" => "application/pdf", ".jpg" or ".jpeg" => "image/jpeg", ".png" => "image/png", _ => "" };
    internal static async Task<byte[]> BytesAsync(string root, string archive, FileCandidate file)
    {
        var parts = file.SourcePath.Replace('\\', '/').Split('/'); Need(!Path.IsPathRooted(file.SourcePath) && !parts.Any(x => x is ".." or "." or "" || x.Contains(':')), "Dosya yolu güvensiz.");
        if (file.Kind == "CONTRACT")
        {
            var full = Path.GetFullPath(Path.Combine(root, file.SourcePath)); Need(full.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "Dosya kök dışı."); SafeNoLink(full);
            Need(new FileInfo(full).Length == file.Size, "Yerel boyut değişmiş."); return await File.ReadAllBytesAsync(full);
        }
        Need(parts.Length == 3 && parts[0] == "Aktivite", "Aktivite arşiv yolu güvensiz.");
        var start = new ProcessStartInfo("7z") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "e", "-so", "-spd", archive, file.SourcePath }) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!; var error = process.StandardError.ReadToEndAsync(); using var bytes = new MemoryStream(); var buffer = new byte[65536];
        try { int n; while ((n = await process.StandardOutput.BaseStream.ReadAsync(buffer)) > 0) { Need(bytes.Length + n <= 20971520, "Arşiv çıktısı sınırı aşıldı."); await bytes.WriteAsync(buffer.AsMemory(0, n)); } await process.WaitForExitAsync(); await error; Need(process.ExitCode == 0, "Arşiv okuma hatası."); return bytes.ToArray(); }
        catch { if (!process.HasExited) process.Kill(true); throw; }
    }
    internal static void ValidateBytes(FileCandidate f, byte[] bytes)
    {
        Need(bytes.LongLength == f.Size && Convert.ToHexString(SHA256.HashData(bytes)) == f.ContentHash, "Yerel içerik hash/boyut değişmiş.");
        var signature = f.ContentType switch { "application/pdf" => bytes.Length >= 5 && bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8), "image/png" => bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137,80,78,71,13,10,26,10 }), "image/jpeg" => bytes.Length >= 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255, _ => false };
        Need(signature, "Dosya içerik imzası geçersiz.");
    }
    internal static FilePlan LoadPlan(string path, string hash, string scope)
    {
        path = Private(path); Need(HashFile(path) == hash, "Dosya plan hash farklı."); var p = JsonSerializer.Deserialize<FilePlan>(File.ReadAllText(path))!;
        Need(p.Version == Version && p.Scope == scope && p.CodeHash == CodeHash(), "Dosya kapsamı/kod sürümü farklı."); VerifyBundle(Private(p.Bundle), p.BundleHash);
        Need(HashFile(Private(p.FinancialAcceptance)) == p.FinancialAcceptanceHash && HashFile(p.ActivityArchive) == p.ActivityHash, "Finansal kanıt/arşiv değişmiş."); return p;
    }
    internal static async Task ValidateOwnerAsync(AppDataContext db, FilePlan p, FileCandidate f)
    {
        var batch = await db.Set<CollectionMigrationBatch>().AsNoTracking().SingleAsync(x => x.Id == p.BatchId);
        Need(batch.Status == CollectionMigrationBatchStatus.Reconciled && Convert.ToHexString(batch.RowVersion) == p.BatchVersion && Convert.ToHexString(batch.ManifestHash) == p.BundleHash, "Finansal batch değişmiş.");
        var evidence = await db.Set<CollectionMigrationSourceRow>().AsNoTracking().SingleAsync(x => x.Id == f.EvidenceId && x.BatchId == p.BatchId);
        Need(Convert.ToHexString(evidence.PayloadHash) == f.EvidenceHash && HashText(evidence.Payload) == f.EvidenceHash, "Dosya sahiplik audit'i değişmiş.");
        var owners = await CollectionCustomerScopeQuery.Customers(db.Customers.AsNoTracking()).Where(x => x.SubscriberCode != null && x.SubscriberCode.Trim() == f.Subscriber).Select(x => x.Id).ToArrayAsync();
        Need(owners.Length == 1 && owners[0] == f.CustomerId, "Dosya müşteri eşlemesi değişmiş.");
        if (f.ContractId.HasValue)
        {
            var c = await db.Set<CollectionContract>().AsNoTracking().SingleAsync(x => x.Id == f.ContractId);
            var map = await db.Set<CollectionMigrationMap>().AsNoTracking().SingleAsync(x => x.SourceSystem == "MGS" && x.EntityCode == "Contract" && x.SourceId == f.SourceId && x.FirstBatchId == p.BatchId);
            Need(!c.IsDeleted && c.CustomerId == f.CustomerId && Convert.ToHexString(c.RowVersion) == f.ContractVersion && map.TargetContractId == c.Id, "Dosya sözleşme sahibi/sürümü değişmiş.");
        }
    }
    internal static async Task JournalAsync(string output, string state, string planHash, FilePlan plan, FileCandidate file, object? detail = null)
    { await File.AppendAllTextAsync(Path.Combine(output, "journal.ndjson"), JsonSerializer.Serialize(new { AtUtc = DateTimeOffset.UtcNow, State = state, PlanHash = planHash, plan.BatchId, file.Kind, file.SourceId, file.EvidenceHash, file.LegacyOwner, file.RetainedOwner, file.CustomerId, file.ContractId, file.ContractVersion, plan.ActivityHash, file.ContentHash, file.Size, file.ContentType, file.OriginalName, file.StoredName, plan.NamespaceHash, Detail = detail }) + "\n"); }
    internal static async Task<AttachResult> OneAsync(AppDataContext db, FilePlan p, FileCandidate f, FileTransport transport, string output, string planHash, string? fault = null)
    {
        await ValidateOwnerAsync(db, p, f);
        var receiptExists = await db.Set<CollectionMigrationSourceRow>().AnyAsync(x => x.BatchId == p.BatchId && x.EntityCode == "CdnFileReceipt" && x.SourceId == f.Kind + ":" + f.SourceId);
        if (f.Kind == "CONTRACT")
        {
            var old = await db.Set<CollectionContractAttachment>().AsNoTracking().SingleOrDefaultAsync(x => x.StoredFileName == f.StoredName);
            Need(old is null ? !receiptExists : !old.IsDeleted && old.ContractId == f.ContractId && old.OriginalFileName == f.OriginalName && old.ContentType == f.ContentType && old.SizeBytes == f.Size && receiptExists, "Dosya silinmiş veya metadata/receipt çelişiyor; CDN çağrılmadı.");
        }
        else
        {
            var old = await db.Set<CollectionCustomerAttachment>().AsNoTracking().SingleOrDefaultAsync(x => x.SourcePath == f.SourcePath);
            Need(old is null ? !receiptExists : !old.IsDeleted && old.CustomerId == f.CustomerId && old.StoredFileName == f.StoredName && old.ContentHash == f.ContentHash && receiptExists, "Müşteri dosyası silinmiş veya metadata/receipt çelişiyor; CDN çağrılmadı.");
        }
        var bytes = await BytesAsync(p.Root, p.ActivityArchive, f); ValidateBytes(f, bytes);
        await JournalAsync(output, "LocalVerified", planHash, p, f);
        var existed = await transport.Storage.ExistsAsync(f.StoredName);
        if (!existed) { using var stream = new MemoryStream(bytes, false); await transport.Storage.UploadAsync(f.StoredName, stream, f.ContentType); }
        await transport.VerifyAsync(f, false); await JournalAsync(output, "ObjectVerified", planHash, p, f, new { ObjectExistedBefore = existed });
        if (fault == "AFTER_UPLOAD") throw new FileGateException("Prova: upload sonrası kesinti.");
        long attachment; bool created = false;
        await using (var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable))
        {
            await ValidateOwnerAsync(db, p, f);
            var key = f.Kind + ":" + f.SourceId;
            var receipt = await db.Set<CollectionMigrationSourceRow>().AsNoTracking().SingleOrDefaultAsync(x => x.BatchId == p.BatchId && x.EntityCode == "CdnFileReceipt" && x.SourceId == key);
            if (f.Kind == "CONTRACT")
            {
                var old = await db.Set<CollectionContractAttachment>().AsNoTracking().SingleOrDefaultAsync(x => x.StoredFileName == f.StoredName);
                if (old is null)
                {
                    Need(receipt is null && !await db.Set<CollectionContractAttachment>().AnyAsync(x => x.ContractId == f.ContractId && x.OriginalFileName == f.OriginalName), "Dosya receipt/same-name çakışması; silinen ilişki yeniden yaratılmaz.");
                    var entity = new CollectionContractAttachment { ContractId = f.ContractId!.Value, StoredFileName = f.StoredName, OriginalFileName = f.OriginalName, SizeBytes = f.Size, ContentType = f.ContentType, CreatedUser = 0, CreatedDate = DateTimeOffset.UtcNow };
                    db.Add(entity); await db.SaveChangesAsync(); attachment = entity.Id; created = true;
                }
                else { Need(!old.IsDeleted && old.ContractId == f.ContractId && old.OriginalFileName == f.OriginalName && old.SizeBytes == f.Size && old.ContentType == f.ContentType && receipt is not null, "Sözleşme dosyası kaldırılmış veya farklı."); attachment = old.Id; }
            }
            else
            {
                var old = await db.Set<CollectionCustomerAttachment>().AsNoTracking().SingleOrDefaultAsync(x => x.SourcePath == f.SourcePath);
                if (old is null)
                {
                    Need(receipt is null, "Silinen müşteri dosyası yeniden yaratılmaz.");
                    var entity = new CollectionCustomerAttachment { CustomerId = f.CustomerId, LegacyCustomerId = f.LegacyOwner, RetainedLegacyCustomerId = f.RetainedOwner,
                        SourcePath = f.SourcePath, ArchiveHash = p.ActivityHash, ContentHash = f.ContentHash, StoredFileName = f.StoredName, OriginalFileName = f.OriginalName,
                        SizeBytes = f.Size, ContentType = f.ContentType, CreatedUser = 0, CreatedDate = DateTimeOffset.UtcNow, Decision = Version + "; plan=" + planHash + "; yalnız doğrulanmış dosya devri, ödeme aktarılmaz." };
                    db.Add(entity); await db.SaveChangesAsync(); attachment = entity.Id; created = true;
                }
                else { Need(!old.IsDeleted && old.CustomerId == f.CustomerId && old.LegacyCustomerId == f.LegacyOwner && old.RetainedLegacyCustomerId == f.RetainedOwner && old.ContentHash == f.ContentHash && old.ArchiveHash == p.ActivityHash && old.StoredFileName == f.StoredName && old.SizeBytes == f.Size && old.ContentType == f.ContentType && old.OriginalFileName == f.OriginalName && receipt is not null, "Müşteri dosyası kaldırılmış veya farklı."); attachment = old.Id; }
            }
            var receiptPayload = JsonSerializer.Serialize(new { File = f, p.NamespaceHash, AttachmentId = attachment });
            if (receipt is null) { db.Add(new CollectionMigrationSourceRow { BatchId = p.BatchId, EntityCode = "CdnFileReceipt", SourceId = key, Payload = receiptPayload, PayloadHash = Convert.FromHexString(HashText(receiptPayload)), StagedDate = DateTimeOffset.UtcNow }); await db.SaveChangesAsync(); }
            else Need(receipt.Payload == receiptPayload && HashText(receipt.Payload) == Convert.ToHexString(receipt.PayloadHash), "Dosya receipt değişmiş.");
            if (fault == "BEFORE_COMMIT") throw new FileGateException("Prova: SQL commit öncesi kesinti.");
            await tx.CommitAsync();
        }
        db.ChangeTracker.Clear();
        if (fault == "AFTER_COMMIT") throw new FileGateException("Prova: commit sonrası journal öncesi kesinti.");
        await JournalAsync(output, "Attached", planHash, p, f, new { AttachmentId = attachment, Created = created });
        var publicHash = await transport.VerifyAsync(f, true);
        await JournalAsync(output, transport.Isolated ? "SimulatedPublicReadback" : "PublicVerified", planHash, p, f, new { Hash = publicHash, AttachmentId = attachment });
        return new(attachment, created, existed);
    }
    internal static async Task ApplyLiveAsync(string config, string planFile, string planHash, string approvalFile, string approvalHash, string output, string acknowledgement)
    {
        // All authorization and proof checks precede any SQL connection or provider construction.
        Need(acknowledgement == "APPLY_LIVE_FILES", "Canlı dosya aktarımı teyidi yok.");
        approvalFile = Private(approvalFile); Need(HashFile(approvalFile) == approvalHash, "Onay hash farklı."); var a = Doc(approvalFile);
        Need(Text(a, "Authorization") == "EXPLICIT_PRODUCTION_FILE_APPROVAL" && Text(a, "Scope") == "LIVE" && Text(a, "PlanHash") == planHash, "Canlı dosya uygulaması açıkça onaylanmadı.");
        Need(a.GetProperty("ExpiresAtUtc").GetDateTimeOffset() > DateTimeOffset.UtcNow && Text(a, "OperatorCode").Length > 0 && Text(a, "ApprovalReference").Length > 0, "Operatör/onay süresi eksik.");
        foreach (var field in new[] { "MaintenanceApproved", "BackupApproved", "ScanApproved", "PublicExposureAccepted", "NamespaceApproved", "FinancialReplayApproved" }) Need(a.GetProperty(field).GetBoolean(), "Canlı dosya onay kapısı kapalı: " + field);
        foreach (var kind in new[] { "HumanApproval", "Maintenance", "Backup", "MalwareScan", "Namespace", "FinancialReplay" })
        { var e = a.GetProperty("Evidence").EnumerateArray().Single(x => Text(x, "Kind") == kind); Need(HashFile(Private(Text(e, "Path"))) == Text(e, "Sha256"), "Canlı kanıt hash farklı."); }
        var p = LoadPlan(planFile, planHash, "LIVE"); Need(DateTimeOffset.UtcNow - p.PlannedAtUtc < TimeSpan.FromMinutes(30), "Güncel dosya/owner planı gerekli.");
        var acceptancePath = Private(Text(a, "RehearsalAcceptancePath")); Need(HashFile(acceptancePath) == Text(a, "RehearsalAcceptanceHash"), "Dosya prova kabul hash farklı."); var proof = Doc(acceptancePath);
        Need(proof.GetProperty("WritePathVerified").GetBoolean() && Text(proof, "CodeHash") == p.CodeHash && Text(proof, "SourceProjectionHash") == p.SourceProjectionHash && Text(proof, "BundleHash") == p.BundleHash, "Aynı kod/kaynak için dosya provası yok.");
        foreach (var e in proof.GetProperty("Evidence").EnumerateArray()) Need(HashFile(Private(Text(e, "Path"))) == Text(e, "Sha256"), "Dosya prova kanıtı değişmiş.");
        var options = LiveOptions(config); Need(NamespaceHash(options) == p.NamespaceHash, "Canlı namespace farklı.");
        var max = (int)Num(a, "MaxFiles"); Need(max > 0 && max <= p.Files.Length && Text(a, "Mode") is "PILOT" or "FULL" && (Text(a, "Mode") != "FULL" || max == p.Files.Length), "Pilot/tam aktarım sınırı onaysız.");
        await using var db = await OpenAsync(config, "LIVE"); await LockAsync(db);
        Need(await db.Users.CountAsync(x => x.Code == Text(a, "OperatorCode") && !x.IsDeleted) == 1, "Operatör aktif/tekil değil.");
        await db.Database.ExecuteSqlRawAsync("IF (SELECT COUNT(*) FROM msdb.dbo.sysjobs WHERE name IN(N'Platform Tahsilat Bakım',N'Platform SIMStoMGS-MANITOUTOMGS',N'Manitou2Flowassist',N'Platform GrupTopluMail'))<>4 OR EXISTS(SELECT 1 FROM msdb.dbo.sysjobs WHERE enabled=1 AND name IN(N'Platform Tahsilat Bakım',N'Platform SIMStoMGS-MANITOUTOMGS',N'Manitou2Flowassist',N'Platform GrupTopluMail')) OR EXISTS(SELECT 1 FROM msdb.dbo.sysjobactivity a JOIN msdb.dbo.sysjobs j ON j.job_id=a.job_id WHERE a.session_id=(SELECT MAX(session_id) FROM msdb.dbo.syssessions) AND a.start_execution_date IS NOT NULL AND a.stop_execution_date IS NULL AND j.name IN(N'Platform Tahsilat Bakım',N'Platform SIMStoMGS-MANITOUTOMGS',N'Manitou2Flowassist',N'Platform GrupTopluMail')) THROW 51000,N'Job bakım kapısı kapalı.',1;");
        var dir = NewOutput(output);
        using var transport = new FileTransport(new AmazonS3Client(new BasicAWSCredentials(options.AccessKeyId, options.SecretAccessKey), new AmazonS3Config { ServiceURL = options.Endpoint, ForcePathStyle = true }), options, false);
        int created = 0; foreach (var file in p.Files.Take(max)) { var r = await OneAsync(db, p, file, transport, dir, planHash); if (r.Created) created++; }
        await Save(Path.Combine(dir, "result.json"), new { Target = "AssistFlow", PlanHash = planHash, Created = created, Existing = max - created, PublicReadbacks = max, UiAcceptanceCompleted = false, ProductionReleaseApproved = false });
    }
}
