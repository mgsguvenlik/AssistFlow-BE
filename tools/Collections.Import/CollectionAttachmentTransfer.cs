using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Business.Services.Crm.Collections;
using Business.Services.Storage;
using Core.Settings.Concrete;
using Data.Concrete.EfCore.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Model.Concrete.Collections;

/// <summary>Salt-okunur önizleme; hash onayıyla yalnız test dosya ilişkilerini aktarır.</summary>
internal static class CollectionAttachmentTransfer
{
    private const string ArchiveHash = "A35F009E7BEAF858DAFB510BC98E5518F37065F305A69D90CDF0D7A97A0EDA36";
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private sealed record FileItem(string RelativePath, long Size, string SHA256, string? ContentType, string Status);
    private sealed record FileManifest(string ArchiveSHA256, string Root, List<FileItem> Files);
    private sealed record LiveContract(string Customer, string Name, string Path);
    private sealed record Candidate(string LegacyId, long SourceRowId, long ContractId, long CustomerId,
        string SourceHash, string StageVersion, string ContractVersion, string RelativePath,
        string OriginalName, long Size, string SHA256, string ContentType, string StoredName);
    private sealed record ExceptionRow(string LegacyId, long? ContractId, string Stage, string Reason, string? Path);

    public static async Task RunAsync(string settingsPath, string manifestPath, string? expectedHash, int limit)
    {
        Require(expectedHash is null || limit > 0, "Uygulama için pozitif işlem sınırı gerekli.");
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath), new JsonDocumentOptions
            { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        var connection = new SqlConnectionStringBuilder(settings.RootElement.GetProperty("AppSettings").GetProperty("MSSQLConnectionString").GetString());
        Require(connection.DataSource == "192.168.1.8" && connection.InitialCatalog == "AssistFlowTest", "Yalnız AssistFlowTest hedeflenebilir.");
        var options = settings.RootElement.GetProperty("R2Storage").Deserialize<R2StorageOptions>(Json)!;
        Require(options.KeyPrefix == "uploads-test" && new Uri(options.PublicBaseUrl).Host == "cdn.flowassist.mgs.com.tr"
            && new Uri(options.PublicBaseUrl).Scheme == "https" && new Uri(options.Endpoint).Scheme == "https"
            && new Uri(options.Endpoint).Host.EndsWith(".r2.cloudflarestorage.com", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(options.BucketName) && !string.IsNullOrWhiteSpace(options.AccessKeyId)
            && !string.IsNullOrWhiteSpace(options.SecretAccessKey), "Beklenen test CDN ayarları yok.");
        var manifest = JsonSerializer.Deserialize<FileManifest>(await File.ReadAllTextAsync(manifestPath), Json)!;
        Require(manifest.ArchiveSHA256 == ArchiveHash, "İncelenen arşiv manifesti değil.");
        var root = Path.GetFullPath(manifest.Root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var files = manifest.Files.ToDictionary(x => Normalize(x.RelativePath), StringComparer.OrdinalIgnoreCase);
        // İçerik değişikliğini hem önizlemede hem her yüklemenin öncesinde tespit et.
        foreach (var f in files.Values.Where(x => x.Status == "Valid")) await VerifyLocalAsync(root, f);
        var dbOptions = new DbContextOptionsBuilder<AppDataContext>().UseSqlServer(connection.ConnectionString,
            sql => sql.CommandTimeout(180)).Options;
        await using var db = new AppDataContext(dbOptions);
        await db.Database.OpenConnectionAsync();
        if (expectedHash is not null)
            await db.Database.ExecuteSqlRawAsync("""
                DECLARE @r int;
                EXEC @r=sys.sp_getapplock @Resource=N'CollectionLegacyContractApply', @LockMode=N'Exclusive', @LockOwner=N'Session', @LockTimeout=15000;
                IF @r<0 THROW 51000,N'Aktarım kilidi alınamadı.',1;
                """);
        try
        {
            var batch = await db.Set<CollectionMigrationBatch>().AsNoTracking().SingleAsync(x => x.Id == 1);
            Require(batch.SourceSystem == "MGS" && batch.SnapshotKey == "mgs-20260919-131119Z"
                && batch.Status == CollectionMigrationBatchStatus.NeedsReview, "Kesit durumu değişmiş.");
            var stages = await db.Set<CollectionMigrationContractStage>().AsNoTracking().Include(x => x.SourceRow)
                .Where(x => x.SourceRow.BatchId == 1).ToListAsync();
            var maps = await db.Set<CollectionMigrationMap>().AsNoTracking()
                .Where(x => x.SourceSystem == "MGS" && x.EntityCode == "Contract" && x.FirstBatchId == 1).ToDictionaryAsync(x => x.SourceId);
            var contracts = await db.Set<CollectionContract>().AsNoTracking().Include(x => x.Customer)
                .ToDictionaryAsync(x => x.Id);
            var attachments = await db.Set<CollectionContractAttachment>().AsNoTracking().ToListAsync();
            var live = await ReadLiveAsync(connection);
            var candidates = new List<Candidate>();
            var exceptions = new List<ExceptionRow>();
            var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var stage in stages.OrderBy(x => x.SourceRowId))
            {
                var id = stage.SourceRow.SourceId;
                var path = Normalize(stage.AttachmentPath);
                var rel = path.StartsWith("~/Content/Contract/", StringComparison.OrdinalIgnoreCase) ? path[10..] : null;
                if (rel is not null) referenced.Add(rel);
                maps.TryGetValue(id, out var map);
                void Hold(string reason) => exceptions.Add(new(id, map?.TargetContractId, stage.Status.ToString(), reason, rel));
                if (stage.Status != CollectionMigrationRowStatus.Applied) { Hold(stage.Status == CollectionMigrationRowStatus.Excluded ? "Kapsam dışı sözleşme" : "Sözleşme aktarımı bekliyor"); continue; }
                Require(map?.TargetContractId is not null && contracts.ContainsKey(map.TargetContractId.Value), "Aktarılmış sözleşmenin hedefi yok.");
                var target = contracts[map!.TargetContractId!.Value];
                if (target.IsDeleted || target.Customer.IsDeleted || target.CustomerId != stage.TargetCustomerId) { Hold("Hedef müşteri/sözleşme değişmiş"); continue; }
                Require(map.AppliedPayloadHash.SequenceEqual(stage.SourceRow.PayloadHash)
                    && SHA256.HashData(Encoding.UTF8.GetBytes(stage.SourceRow.Payload)).SequenceEqual(stage.SourceRow.PayloadHash), "Kaynak hash uyuşmazlığı.");
                if (!live.TryGetValue(id, out var source) || Normalize(source.Path) != path
                    || source.Name != (stage.AttachmentName ?? "") || source.Customer != stage.SourceCustomerId?.Trim()) { Hold("Canlı kaynak dosya bağlantısı değişmiş"); continue; }
                if (string.IsNullOrWhiteSpace(path)) { Hold("Dosya metadata yok"); continue; }
                if (rel is null || rel.Split('/').Contains("..") || !files.TryGetValue(rel, out var file)) { Hold("Dosya yolu arşivde bulunamadı"); continue; }
                if (file.Status != "Valid") { Hold("Dosya uygun değil: " + file.Status); continue; }
                var original = Path.GetFileName(stage.AttachmentName);
                if (string.IsNullOrWhiteSpace(original) || original.Length > 260
                    || !string.Equals(Mime(Path.GetExtension(original)), file.ContentType, StringComparison.Ordinal)) { Hold("Orijinal dosya adı/türü uyuşmuyor"); continue; }
                var key = "mgs-test-" + Hash($"attachments-v1|AssistFlowTest|{id}|{target.Id}|{Normalize(rel)}|{file.SHA256}").ToLowerInvariant() + Path.GetExtension(rel).ToLowerInvariant();
                if (attachments.Any(x => x.ContractId == target.Id && x.OriginalFileName == original && x.StoredFileName != key)) { Hold("Hedefte aynı adlı farklı dosya var"); continue; }
                candidates.Add(new(id, stage.SourceRowId, target.Id, target.CustomerId, Convert.ToHexString(stage.SourceRow.PayloadHash),
                    Convert.ToHexString(stage.RowVersion), Convert.ToHexString(target.RowVersion), rel, original,
                    file.Size, file.SHA256, file.ContentType!, key));
            }
            var hash = Hash(JsonSerializer.Serialize(new { Version = "attachments-v1", ArchiveHash,
                BatchHash = Convert.ToHexString(batch.ManifestHash), options.Endpoint, options.BucketName,
                options.KeyPrefix, options.PublicBaseUrl, Candidates = candidates }, Json));
            var outDir = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(manifestPath))!, "item-eight-transfer");
            Directory.CreateDirectory(outDir);
            var planPath = Path.Combine(outDir, $"plan-{hash}.json");
            if (!File.Exists(planPath)) await File.WriteAllTextAsync(planPath, JsonSerializer.Serialize(new { Hash = hash, ArchiveHash, Candidates = candidates, Exceptions = exceptions,
                UnreferencedFiles = files.Keys.Where(x => !referenced.Contains(x)).Order().ToArray() }, Json));
            var existing = attachments.ToDictionary(x => x.StoredFileName, StringComparer.Ordinal);
            foreach (var row in candidates.Where(x => existing.ContainsKey(x.StoredName))) CheckAttachment(row, existing[row.StoredName]);
            var pending = candidates.Where(x => !existing.ContainsKey(x.StoredName)).ToList();
            Console.WriteLine($"Dosya planı: uygun ilişki={candidates.Count}; mevcut={candidates.Count - pending.Count}; yeni={pending.Count}; fiziksel dosya={candidates.Select(x => x.RelativePath).Distinct(StringComparer.OrdinalIgnoreCase).Count()}; bayt={pending.Sum(x => x.Size)}.");
            foreach (var group in exceptions.Where(x => x.Stage == "Applied").GroupBy(x => x.Reason)) Console.WriteLine($"  {group.Key}: {group.Count()}");
            Console.WriteLine($"Plan SHA-256: {hash}");
            Console.WriteLine($"Kanıt: {planPath}");
            if (expectedHash is null) { Console.WriteLine("DRY-RUN; CDN/DB yazması yapılmadı."); return; }
            Require(hash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase), "Onaylı plan değişmiş.");
            using var client = new AmazonS3Client(new BasicAWSCredentials(options.AccessKeyId, options.SecretAccessKey),
                new AmazonS3Config { ServiceURL = options.Endpoint, ForcePathStyle = true });
            var storage = new R2FileStorage(client, Options.Create(options), NullLogger<R2FileStorage>.Instance);
            var journalPath = Path.Combine(outDir, "journal.ndjson");
            using var journalLock = new SemaphoreSlim(1, 1);
            async Task Journal(object value)
            {
                await journalLock.WaitAsync();
                try
                {
                    await using var log = new FileStream(journalPath, FileMode.Append, FileAccess.Write, FileShare.Read);
                    await log.WriteAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value) + "\n"));
                    log.Flush(true);
                }
                finally { journalLock.Release(); }
            }
            var completed = 0;
            Candidate? lastCompleted = null;
            // Önce görsel ve küçük PDF; aynı dosyayı kullanan ilişkiler normal deterministik sırada gelir.
            foreach (var chunk in pending.OrderBy(x => x.ContentType == "application/pdf" ? 1 : 0).ThenBy(x => x.Size).ThenBy(x => x.ContractId).Take(limit).Chunk(4))
            {
                // Yalnız ağ/dosya işleri paralel; EF context ve transaction'lar seri kullanılır.
                await Task.WhenAll(chunk.Select(async row =>
                {
                    await VerifyLocalAsync(root, files[row.RelativePath]);
                    await Journal(new { At = DateTimeOffset.UtcNow, State = "Started", PlanHash = hash, row.LegacyId, row.ContractId, row.StoredName, row.SHA256 });
                    if (!await storage.ExistsAsync(row.StoredName))
                    {
                        await using var input = File.OpenRead(SafePath(root, row.RelativePath));
                        await storage.UploadAsync(row.StoredName, input, row.ContentType);
                    }
                    await VerifyRemoteAsync(client, options, row);
                    await Journal(new { At = DateTimeOffset.UtcNow, State = "CdnVerified", row.ContractId, row.StoredName, row.SHA256 });
                }));
                foreach (var row in chunk)
                {
                    await using (var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable))
                    {
                        var current = await db.Set<CollectionContract>().AsNoTracking().Include(x => x.Customer).SingleAsync(x => x.Id == row.ContractId);
                        var currentStage = await db.Set<CollectionMigrationContractStage>().AsNoTracking().SingleAsync(x => x.SourceRowId == row.SourceRowId);
                        Require(!current.IsDeleted && !current.Customer.IsDeleted && current.CustomerId == row.CustomerId
                            && Convert.ToHexString(current.RowVersion) == row.ContractVersion
                            && currentStage.Status == CollectionMigrationRowStatus.Applied
                            && Convert.ToHexString(currentStage.RowVersion) == row.StageVersion, "Aktarım sırasında hedef/kapsam değişmiş; CDN nesnesi korunarak duruldu.");
                        var attached = await db.Set<CollectionContractAttachment>().SingleOrDefaultAsync(x => x.StoredFileName == row.StoredName);
                        if (attached is null)
                        {
                            Require(!await db.Set<CollectionContractAttachment>().AnyAsync(x => x.ContractId == row.ContractId && x.OriginalFileName == row.OriginalName), "Hedefte aynı adlı dosya eşzamanlı oluşturuldu.");
                            db.Add(new CollectionContractAttachment { ContractId = row.ContractId, OriginalFileName = row.OriginalName,
                                StoredFileName = row.StoredName, ContentType = row.ContentType, SizeBytes = row.Size,
                                // Diğer legacy aktarımlarındaki sistem audit kimliği; gerçek kullanıcı taklit edilmez.
                                CreatedUser = 0, CreatedDate = DateTimeOffset.UtcNow });
                            await db.SaveChangesAsync();
                        }
                        else CheckAttachment(row, attached);
                        await tx.CommitAsync();
                    }
                    db.ChangeTracker.Clear();
                    await Journal(new { At = DateTimeOffset.UtcNow, State = "Attached", row.LegacyId, row.ContractId, row.StoredName, row.SHA256 });
                    lastCompleted = row;
                    completed++;
                    if (completed <= 5 || completed % 10 == 0) Console.WriteLine($"Doğrulandı ve bağlandı: {completed}/{Math.Min(limit, pending.Count)}.");
                }
            }
            // Mevcut servisle liste modeli ve URL üretimi korunuyor.
            var sample = lastCompleted ?? candidates.FirstOrDefault(x => existing.ContainsKey(x.StoredName));
            if (sample is not null)
            {
                var service = new CollectionContractAttachmentService(db, storage, NullLogger<CollectionContractAttachmentService>.Instance);
                var result = await service.GetPageAsync(sample.ContractId, 1, 20);
                Require(result.IsSuccess && result.Data?.Items.Any(x => x.Url == storage.GetPublicUrl(sample.StoredName)
                    && x.OriginalFileName == sample.OriginalName && x.SizeBytes == sample.Size) == true,
                    "Mevcut sözleşme servisi dosya ilişkisini doğru listeleyemedi.");
                await File.WriteAllTextAsync(Path.Combine(outDir, "service-list-check.json"), JsonSerializer.Serialize(result, Json));
            }
            Console.WriteLine($"Tamamlandı: bu çalıştırmada {completed} ilişki. Kaynak silme, ortak tablo ve CDN altyapı değişikliği yok.");
        }
        finally
        {
            if (expectedHash is not null)
                await db.Database.ExecuteSqlRawAsync("EXEC sys.sp_releaseapplock @Resource=N'CollectionLegacyContractApply', @LockOwner=N'Session';");
        }
    }

    private static async Task<Dictionary<string, LiveContract>> ReadLiveAsync(SqlConnectionStringBuilder target)
    {
        var source = new SqlConnectionStringBuilder(target.ConnectionString) { InitialCatalog = "MGS" };
        await using var con = new SqlConnection(source.ConnectionString);
        await con.OpenAsync();
        await using var cmd = con.CreateCommand();
        cmd.CommandTimeout = 90;
        cmd.CommandText = "SELECT ContractID,CustomerID,FileAttachmentName,FileAttachmentPath FROM Core.Contract";
        await using var reader = await cmd.ExecuteReaderAsync();
        var rows = new Dictionary<string, LiveContract>();
        while (await reader.ReadAsync()) rows.Add(reader[0].ToString()!, new(reader[1].ToString()!, reader[2].ToString()!, reader[3].ToString()!));
        return rows;
    }

    private static async Task VerifyLocalAsync(string root, FileItem f)
    {
        var path = SafePath(root, f.RelativePath);
        var info = new FileInfo(path);
        Require(info.Exists && (info.Attributes & FileAttributes.ReparsePoint) == 0 && info.Length == f.Size
            && f.Size is > 0 and <= 20971520 && Mime(info.Extension) == f.ContentType, "Yerel dosya boyutu/türü değişmiş.");
        await using var stream = File.OpenRead(path);
        var header = new byte[8];
        var len = await stream.ReadAsync(header);
        var valid = f.ContentType switch
        {
            "application/pdf" => len >= 5 && header.AsSpan(0, 5).SequenceEqual("%PDF-"u8),
            "image/png" => len == 8 && Convert.ToHexString(header) == "89504E470D0A1A0A",
            "image/jpeg" => len >= 3 && header[0] == 255 && header[1] == 216 && header[2] == 255,
            _ => false
        };
        Require(valid, "Yerel dosya imzası geçersiz.");
        stream.Position = 0;
        Require(Convert.ToHexString(await SHA256.HashDataAsync(stream)) == f.SHA256, "Yerel dosya hash'i değişmiş.");
    }

    private static async Task VerifyRemoteAsync(IAmazonS3 client, R2StorageOptions options, Candidate row)
    {
        using var response = await client.GetObjectAsync(new GetObjectRequest { BucketName = options.BucketName, Key = options.KeyPrefix + "/" + row.StoredName });
        Require(response.ContentLength == row.Size && response.Headers.ContentType == row.ContentType, "CDN boyut/tür uyuşmazlığı; nesnenin üzerine yazılmadı.");
        Require(Convert.ToHexString(await SHA256.HashDataAsync(response.ResponseStream)) == row.SHA256, "CDN içerik hash'i uyuşmuyor; otomatik silme yapılmadı.");
    }

    private static void CheckAttachment(Candidate row, CollectionContractAttachment entity) => Require(
        !entity.IsDeleted && entity.ContractId == row.ContractId && entity.OriginalFileName == row.OriginalName
        && entity.ContentType == row.ContentType && entity.SizeBytes == row.Size, "Mevcut dosya ilişkisi onaylı planla çelişiyor.");
    private static string SafePath(string root, string relative)
    {
        Require(!Path.IsPathRooted(relative) && !relative.Contains(':') && !Normalize(relative).Split('/').Contains(".."), "Güvensiz dosya yolu.");
        var full = Path.GetFullPath(Path.Combine(root, relative));
        Require(full.StartsWith(root, StringComparison.OrdinalIgnoreCase), "Kaynak klasörü dışında dosya.");
        return full;
    }
    private static string Normalize(string? path) => string.Join('/', (path ?? "").Trim().Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries));
    private static string? Mime(string extension) => extension.ToLowerInvariant() switch
        { ".pdf" => "application/pdf", ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", _ => null };
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
