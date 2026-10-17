using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Business.Services.Storage;
using Core.Settings.Concrete;
using Data.Concrete.EfCore.Collections;
using Data.Concrete.EfCore.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Model.Concrete.Collections;

internal static class CollectionActivityTransfer
{
    private const string ArchiveHash = "398B4120D946D7969A3569771DE8E03F2E4FDE59A9CCA045CA7A5D611F96C480";
    // The other approved old customer (2206 -> 87400) has no verified target: never substitute 16598.
    private const long Source = 924, Winner = 291401;
    private sealed record FileItem(string Path, string Name, byte[] Bytes, string Hash, string Type, string Stored);
    public static async Task RunAsync(string settingsPath, string archive, string? mode)
    {
        using var cfg = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var cs = new SqlConnectionStringBuilder(cfg.RootElement.GetProperty("AppSettings").GetProperty("MSSQLConnectionString").GetString());
        Require(cs.DataSource == "192.168.1.8" && cs.InitialCatalog == "AssistFlowTest", "Yalnız AssistFlowTest kullanılabilir.");
        await using var db = new AppDataContext(new DbContextOptionsBuilder<AppDataContext>().UseSqlServer(cs.ConnectionString).Options);
        Require(!db.Database.HasPendingModelChanges(), "Model/snapshot farklı.");
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToArray();
        if (mode == "--install")
        {
            Require(pending.All(x => x.EndsWith("_AddCollectionCustomerAttachments")), "İlgisiz migration var.");
            await db.Database.MigrateAsync(); Console.WriteLine("Müşteri dosyası test şeması hazır."); return;
        }
        Require(pending.Length == 0, "Önce müşteri dosyası migrationı uygulanmalı.");
        using (var stream = File.OpenRead(archive)) Require(Convert.ToHexString(await SHA256.HashDataAsync(stream)) == ArchiveHash, "Onaylanan arşiv değişmiş.");
        var target = await VerifyTarget(db);
        var files = new List<FileItem>();
        var listing = Encoding.UTF8.GetString(await SevenZip("l", "-slt", "-sccUTF-8", Path.GetFullPath(archive)));
        foreach (var block in Regex.Split(listing.Replace("\r", ""), "\n\n"))
        {
            if (!Regex.IsMatch(block, "^Folder = -$", RegexOptions.Multiline)) continue;
            var path = Regex.Match(block, "^Path = (.+)$", RegexOptions.Multiline).Groups[1].Value;
            var normalized = path.Replace('\\', '/');
            if (!normalized.StartsWith($"Aktivite/{Source}/", StringComparison.Ordinal)) continue;
            Require(normalized.Split('/').Length == 3 && !normalized.Split('/').Contains(".."), "Geçersiz arşiv yolu.");
            var name = normalized.Split('/')[2]; var ext = System.IO.Path.GetExtension(name).ToLowerInvariant();
            var bytes = await SevenZip("e", "-so", "-spd", Path.GetFullPath(archive), path);
            Require(bytes.Length is > 4 and <= 20971520 && name.Length <= 260, "Dosya boyutu/adı geçersiz.");
            var type = ext switch { ".pdf" when bytes.AsSpan(0, 4).SequenceEqual("%PDF"u8) => "application/pdf",
                ".jpg" or ".jpeg" when bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255 => "image/jpeg",
                ".png" when bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) => "image/png", _ => "" };
            Require(type != "", "Dosya içerik imzası/uzantısı geçersiz.");
            var hash = Convert.ToHexString(SHA256.HashData(bytes));
            files.Add(new(normalized, name, bytes, hash, type, $"collection-activity-{Source}-{hash.ToLowerInvariant()}{ext}"));
        }
        Require(files.Count == 3 && files.Select(x => x.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 3, "Beklenen üç dosya bulunamadı.");
        var plan = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { ArchiveHash, Source, Winner, target,
            Files = files.OrderBy(x => x.Path, StringComparer.Ordinal).Select(x => new { x.Path, x.Hash, x.Type, x.Stored }) })));
        Console.WriteLine($"Plan: legacy {Source} -> korunan {Winner} -> test müşteri {target}; dosya={files.Count}; SHA256={plan}");
        if (mode is null) { Console.WriteLine("Önizleme; DB/CDN değişmedi. 2206 için hedef bulunmadığından 3 dosya bekliyor."); return; }
        Require(mode.Equals(plan, StringComparison.OrdinalIgnoreCase), "Plan hash değişmiş.");
        var opt = cfg.RootElement.GetProperty("R2Storage").Deserialize<R2StorageOptions>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        Require(opt.KeyPrefix == "uploads-test" && new Uri(opt.PublicBaseUrl).Host == "cdn.flowassist.mgs.com.tr" && new Uri(opt.PublicBaseUrl).Scheme == "https"
            && new Uri(opt.Endpoint).Scheme == "https" && new Uri(opt.Endpoint).Host.EndsWith(".r2.cloudflarestorage.com"), "Test CDN ayarları doğrulanamadı.");
        using var client = new AmazonS3Client(new BasicAWSCredentials(opt.AccessKeyId, opt.SecretAccessKey), new AmazonS3Config { ServiceURL = opt.Endpoint, ForcePathStyle = true });
        var storage = new R2FileStorage(client, Options.Create(opt), NullLogger<R2FileStorage>.Instance);
        var created = 0; var existing = 0;
        foreach (var file in files)
        {
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            Require(await VerifyTarget(db) == target, "Hedef eşlemesi değişti.");
            var old = await db.Set<CollectionCustomerAttachment>().AsNoTracking().SingleOrDefaultAsync(x => x.SourcePath == file.Path);
            Require(old == null || old.CustomerId == target && old.LegacyCustomerId == Source && old.RetainedLegacyCustomerId == Winner && old.ContentHash == file.Hash
                && old.ArchiveHash == ArchiveHash && old.StoredFileName == file.Stored && old.SizeBytes == file.Bytes.Length, "Mevcut dosya ilişkisi değişmiş.");
            // Removed files retain their source receipt and must not be reactivated by migration.
            Require(old is null || !old.IsDeleted, "Kullanıcının kaldırdığı dosya tekrar aktifleştirilemez.");
            if (!await storage.ExistsAsync(file.Stored))
            {
                using var content = new MemoryStream(file.Bytes, false);
                await storage.UploadAsync(file.Stored, content, file.Type);
            }
            using var downloaded = await client.GetObjectAsync(new GetObjectRequest { BucketName = opt.BucketName, Key = $"{opt.KeyPrefix}/{file.Stored}" });
            Require(Convert.ToHexString(await SHA256.HashDataAsync(downloaded.ResponseStream)) == file.Hash, "CDN içerik hash doğrulaması başarısız.");
            if (old == null)
            {
                db.Add(new CollectionCustomerAttachment { CustomerId = target, LegacyCustomerId = Source, RetainedLegacyCustomerId = Winner, SourcePath = file.Path,
                    ArchiveHash = ArchiveHash, ContentHash = file.Hash, OriginalFileName = file.Name, StoredFileName = file.Stored, ContentType = file.Type,
                    SizeBytes = file.Bytes.Length, CreatedDate = DateTimeOffset.UtcNow,
                    Decision = "30.09.2026 kullanıcı onayı: elenen eski aboneliğin dosyası doğrulanmış güncel müşteri kartına devredilir; ödemeler MGS'de kalır. Plan=" + plan });
                await db.SaveChangesAsync(); created++;
            }
            else existing++;
            await tx.CommitAsync(); db.ChangeTracker.Clear();
        }
        var page = await new Business.Services.Crm.Collections.CollectionContractAttachmentService(db, storage,
            NullLogger<Business.Services.Crm.Collections.CollectionContractAttachmentService>.Instance).GetCustomerPageAsync(target, 1, 25);
        Require(page.IsSuccess && page.Data!.TotalCount >= 3, "Müşteri dosya listesi doğrulanamadı.");
        Console.WriteLine($"Tamamlandı: yeni={created}, önceden aktarılmış={existing}, hash doğrulanan={files.Count}; müşteri kartı={target}. Ödeme/sözleşme/ortak müşteri tabloları değişmedi.");
    }
    private static async Task<long> VerifyTarget(AppDataContext db)
    {
        var decision = await (from i in db.Set<CollectionMigrationIssue>().AsNoTracking()
                              join s in db.Set<CollectionMigrationContractStage>().AsNoTracking() on i.SourceRowId equals s.SourceRowId
                              where s.SourceCustomerId == "924" && s.Status == CollectionMigrationRowStatus.Excluded && i.IssueCode == "CUSTOMER_DUPLICATE_SUPERSEDED"
                                  && i.Status == CollectionMigrationIssueStatus.Resolved
                              select i.ResolutionNote).ToListAsync();
        Require(decision.Count > 0 && decision.All(x => x != null && x.Contains("Eski/kaynak müşteri=924; korunan müşteri=291401;")), "Tekilleştirme kararı doğrulanamadı.");
        var subscriber = await db.Database.SqlQuery<string>($"SELECT SubscriberNo AS [Value] FROM MGS.Core.Customer WHERE CustomerID = {Winner}").SingleAsync();
        Require(!string.IsNullOrWhiteSpace(subscriber), "Korunan müşteri abone numarası eksik.");
        var targets = await db.Customers.AsNoTracking().Where(x => x.SubscriberCode != null && x.SubscriberCode.Trim() == subscriber.Trim()).Select(x => x.Id).ToListAsync();
        Require(targets.Count == 1 && await CollectionCustomerScopeQuery.Customers(db.Customers.AsNoTracking()).AnyAsync(x => x.Id == targets[0]), "Korunan müşterinin tekil kapsam içi kartı doğrulanamadı.");
        return targets[0];
    }
    private static async Task<byte[]> SevenZip(params string[] args)
    {
        var info = new ProcessStartInfo("7z") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        using var process = Process.Start(info)!; using var bytes = new MemoryStream();
        var error = process.StandardError.ReadToEndAsync(); await process.StandardOutput.BaseStream.CopyToAsync(bytes); await process.WaitForExitAsync(); await error;
        Require(process.ExitCode == 0, "Arşiv okunamadı."); return bytes.ToArray();
    }
    private static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
}
