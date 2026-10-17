using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Business.Services.Crm.Collections;
using Data.Concrete.EfCore.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Model.Concrete.Collections;
using static FileAdapter;

internal static class FileRehearsal
{
    private static async Task<Dictionary<string, string>> FinancialStateAsync(AppDataContext db)
    {
        var tables = new[] { "Contract", "ContractRatePeriod", "Payment", "PaymentOperation", "CustomerNote", "GroupParent", "ContractPeriodFollowUp", "Invoice", "InvoicePayment", "InvoiceAccount", "BankTransaction", "BankBaseline", "MigrationBatch", "MigrationMap" };
        var keys = new Dictionary<string, string> { ["GroupParent"] = "CustomerId", ["InvoiceAccount"] = "Code", ["BankTransaction"] = "[Key]" };
        var result = new Dictionary<string, string>();
        foreach (var table in tables)
        {
            using var cmd = db.Database.GetDbConnection().CreateCommand(); cmd.CommandTimeout = 120;
            cmd.CommandText = $"SELECT * FROM collection.[{table}] ORDER BY {keys.GetValueOrDefault(table, "Id")}";
            await using var r = await cmd.ExecuteReaderAsync(); using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            while (await r.ReadAsync()) { var row = new Dictionary<string, object?>(); for (int i = 0; i < r.FieldCount; i++) row[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i); hash.AppendData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(row) + "\n")); }
            result[table] = Convert.ToHexString(hash.GetHashAndReset());
        }
        using (var cmd = db.Database.GetDbConnection().CreateCommand())
        {
            cmd.CommandTimeout = 120; cmd.CommandText = "SELECT Id,BatchId,EntityCode,SourceId,PayloadHash FROM collection.MigrationSourceRow WHERE EntityCode<>N'CdnFileReceipt' ORDER BY Id";
            await using var r = await cmd.ExecuteReaderAsync(); using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            while (await r.ReadAsync()) { hash.AppendData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { Id = r.GetInt64(0), Batch = r.GetInt64(1), Entity = r.GetString(2), Source = r.GetString(3), Hash = Convert.ToHexString((byte[])r[4]) }) + "\n")); }
            result["OriginalSourceAudit"] = Convert.ToHexString(hash.GetHashAndReset());
        }
        return result;
    }
    internal static async Task RunAsync(string config, string planFile, string planHash, string output, string acknowledgement)
    {
        Need(acknowledgement == "REHEARSAL_ONLY", "Yalnız izole prova teyidi gereklidir.");
        var p = LoadPlan(planFile, planHash, "REHEARSAL");
        Need(p.NamespaceHash == NamespaceHash(FileTransport.IsolatedOptions()), "İzole namespace farklı.");
        var dir = NewOutput(output); var objects = Path.Combine(dir, "isolated-objects"); Directory.CreateDirectory(objects);
        await using var db = await OpenAsync(config, "REHEARSAL"); await LockAsync(db);
        Need(!await db.Set<CollectionContractAttachment>().AnyAsync() && !await db.Set<CollectionCustomerAttachment>().AnyAsync() && !await db.Set<CollectionMigrationSourceRow>().AnyAsync(x => x.EntityCode == "CdnFileReceipt"), "Tam prova boş attachment baseline'ı gerektirir; eski veri otomatik silinmez.");
        var before = await FinancialStateAsync(db); await Save(Path.Combine(dir, "financial-before.json"), before);
        using var fake = new IsolatedS3(objects); using var transport = new FileTransport(fake, FileTransport.IsolatedOptions(), true);
        var checks = new List<object>();
        var small = p.Files.Where(x => x.Kind == "CONTRACT").OrderBy(x => x.Size).Take(3).ToArray(); Need(small.Length == 3, "Üç sözleşme dosyası gerekli.");
        async Task Rejected(string name, Func<Task> action, bool injected)
        {
            bool rejected = false;
            try { await action(); }
            catch (FileGateException) { rejected = true; }
            catch (Amazon.S3.AmazonS3Exception) when (name == "METADATA_403_NOT_MISSING") { rejected = true; }
            finally { db.ChangeTracker.Clear(); }
            Need(rejected, "Prova reddi gerçekleşmedi: " + name); checks.Add(new { Check = name, Passed = true, ControlledInterruption = injected });
        }
        for (int i = 0; i < 3; i++)
        {
            var fault = new[] { "AFTER_UPLOAD", "BEFORE_COMMIT", "AFTER_COMMIT" }[i]; var f = small[i];
            await Rejected(fault, async () => { await OneAsync(db, p, f, transport, dir, planHash, fault); }, true);
            var puts = fake.Puts; var resumed = await OneAsync(db, p, f, transport, dir, planHash);
            Need(fake.Puts == puts && resumed.Created == (fault != "AFTER_COMMIT"), "Kesinti tekrarında çift upload/ilişki oluştu.");
        }
        var allCreated = 2; // The AFTER_COMMIT row was committed before its simulated journal interruption.
        foreach (var f in p.Files)
        {
            var r = await OneAsync(db, p, f, transport, dir, planHash); if (r.Created) allCreated++;
            if ((allCreated + 1) % 100 == 0) Console.WriteLine("İzole dosya aktarım ilerlemesi: " + (allCreated + 1));
        }
        var putsBeforeReplay = fake.Puts; int replayCreated = 0;
        foreach (var f in p.Files) { var r = await OneAsync(db, p, f, transport, dir, planHash); if (r.Created) replayCreated++; }
        Need(replayCreated == 0 && fake.Puts == putsBeforeReplay, "Tam replay yeni ilişki/upload üretti.");
        checks.Add(new { Check = "FULL_REPLAY_ZERO_INSERT_ZERO_PUT", Passed = true });
        var first = small[0]; var putsBeforeFaults = fake.Puts;
        await Rejected("WRONG_CUSTOMER", async () => { await OneAsync(db, p, first with { CustomerId = -1 }, transport, dir, planHash); }, false);
        await Rejected("EVIDENCE_DRIFT", async () => { await OneAsync(db, p, first with { EvidenceHash = new string('0', 64) }, transport, dir, planHash); }, false);
        fake.CorruptReads = true;
        try { await Rejected("REMOTE_HASH_MISMATCH", async () => { await OneAsync(db, p, first, transport, dir, planHash); }, false); }
        finally { fake.CorruptReads = false; }
        fake.DenyMetadata = true;
        try { await Rejected("METADATA_403_NOT_MISSING", async () => { await OneAsync(db, p, first, transport, dir, planHash); }, false); }
        finally { fake.DenyMetadata = false; }
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            await db.Set<CollectionContractAttachment>().Where(x => x.StoredFileName == first.StoredName).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsDeleted, true));
            await Rejected("REMOVED_ATTACHMENT_NOT_REACTIVATED", async () => { await OneAsync(db, p, first, transport, dir, planHash); }, false);
            await tx.RollbackAsync(); db.ChangeTracker.Clear();
        }
        Need(fake.Puts == putsBeforeFaults, "Olumsuz kontroller nesne yazdı.");
        var service = new CollectionContractAttachmentService(db, transport.Storage, NullLogger<CollectionContractAttachmentService>.Instance);
        var contractPage = await service.GetPageAsync(first.ContractId!.Value, 1, 100);
        Need(contractPage.IsSuccess && contractPage.Data!.Items.Any(x => x.Url == transport.Storage.GetPublicUrl(first.StoredName)), "Mevcut sözleşme dosya servisi uyumsuz.");
        var activity = p.Files.First(x => x.Kind == "CUSTOMER"); var customerPage = await service.GetCustomerPageAsync(activity.CustomerId, 1, 100);
        Need(customerPage.IsSuccess && customerPage.Data!.Items.Any(x => x.Url == transport.Storage.GetPublicUrl(activity.StoredName)), "Mevcut müşteri dosya servisi uyumsuz.");
        checks.Add(new { Check = "EXISTING_CONTRACT_CUSTOMER_LIST_SERVICES", Passed = true });
        var after = await FinancialStateAsync(db); Need(JsonSerializer.Serialize(before) == JsonSerializer.Serialize(after), "Finansal/kaynak audit baseline değişti.");
        await Save(Path.Combine(dir, "financial-after.json"), after);
        var contracts = await db.Set<CollectionContractAttachment>().CountAsync(); var customers = await db.Set<CollectionCustomerAttachment>().CountAsync();
        var receipts = await db.Set<CollectionMigrationSourceRow>().CountAsync(x => x.BatchId == p.BatchId && x.EntityCode == "CdnFileReceipt");
        Need(contracts == p.Files.Count(x => x.Kind == "CONTRACT") && customers == p.Files.Count(x => x.Kind == "CUSTOMER") && receipts == p.Files.Length && fake.Puts == p.Files.Length, "Son adet/receipt/nesne mutabakatı farklı.");
        await Save(Path.Combine(dir, "result.json"), new { Target = "AssistFlow_Performance_Restore", p.BatchId, PlanHash = planHash, Contracts = contracts, Customers = customers, Receipts = receipts, ObjectsWrittenLocally = fake.Puts,
            ReplayCreated = replayCreated, ReplayPutCount = fake.Puts - putsBeforeReplay, FinancialAndOriginalAuditUnchanged = true, Checks = checks,
            RealProviderCalls = 0, PublicHttpVerified = false, UiAndHttpAuthorizationVerified = false, ProductionWrites = false, LocalSimulationOnly = true });
        var evidence = new[] { "result.json", "journal.ndjson", "financial-before.json", "financial-after.json" }.Select(x => new { Path = Path.Combine(dir, x), Sha256 = HashFile(Path.Combine(dir, x)) }).ToArray();
        await Save(Path.Combine(dir, "acceptance.json"), new { Version = FileAdapter.Version, p.BundleHash, p.CodeHash, p.SourceProjectionHash, WritePathVerified = true, ProductionAuthorizationGranted = false, RealCdnReadbackVerified = false, Evidence = evidence });
        Console.WriteLine($"İzole prova tamam: {contracts} sözleşme / {customers} müşteri dosyası / {receipts} receipt. Replay 0; gerçek CDN/HTTP çağrısı yok.");
    }
}
