using System.Text.Json;
using Amazon.Runtime;
using Amazon.S3;
using Business.Services.Crm.Collections;
using Business.Services.Storage;
using ClosedXML.Excel;
using Core.Settings.Concrete;
using Data.Concrete.EfCore.Context;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Model.Concrete.Collections;
using Model.Dtos.Crm.Collections;

internal static class CollectionInvoiceLoadCheck
{
    public static async Task RunAsync(string path)
    {
        using var config = JsonDocument.Parse(await File.ReadAllTextAsync(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var cs = new SqlConnectionStringBuilder(config.RootElement.GetProperty("AppSettings").GetProperty("MSSQLConnectionString").GetString());
        if (cs.DataSource != "192.168.1.8" || cs.InitialCatalog != "AssistFlowTest") throw new InvalidOperationException("Yalnız AssistFlowTest üzerinde doğrulanabilir.");
        var options = config.RootElement.GetProperty("R2Storage").Deserialize<R2StorageOptions>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        using var client = new AmazonS3Client(new BasicAWSCredentials(options.AccessKeyId, options.SecretAccessKey), new AmazonS3Config { ServiceURL = options.Endpoint, ForcePathStyle = true });
        var storage = new R2FileStorage(client, Options.Create(options), NullLogger<R2FileStorage>.Instance);
        await using var db = new AppDataContext(new DbContextOptionsBuilder<AppDataContext>().UseSqlServer(cs.ConnectionString, o => o.CommandTimeout(120)).Options);
        if ((await db.Database.GetPendingMigrationsAsync()).Any() || db.Database.HasPendingModelChanges()) throw new InvalidOperationException("Şema hazır değil.");
        var customer = await db.Set<CollectionInvoiceAccount>().AsNoTracking().Where(x => x.CustomerId != null && x.Issue == null).Select(x => x.CustomerId!.Value).FirstAsync();
        var actor = await db.Users.AsNoTracking().Where(x => !x.IsDeleted).Select(x => x.Id).FirstAsync();
        var marker = ("K07-CHECK-" + Guid.NewGuid().ToString("N")).ToUpperInvariant();
        db.Add(new CollectionInvoiceAccount { Code = marker, CustomerId = customer, SourceHash = marker, SourceIdsJson = "[]", VerifiedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var service = new CollectionInvoiceLoadService(db, storage, NullLogger<CollectionInvoiceLoadService>.Instance);
        var files = new HashSet<string>(); var loads = new List<long>(); long? invoiceId = null;
        var request = Guid.NewGuid();
        byte[] Workbook(string note)
        {
            using var wb = new XLWorkbook(); var ws = wb.AddWorksheet("Faturalar");
            string[] headers = ["Cari Kod", "Müşteri Adı", "No", "Tarih", "Tutar", "Ödendi mi", "Açıklama", "Proje Kodu", "Para Birimi"];
            for (var c = 0; c < headers.Length; c++) ws.Cell(1, c + 1).Value = headers[c];
            for (var r = 2; r <= 3; r++)
            {
                ws.Cell(r, 1).Value = r == 2 ? marker : marker + "-MISSING";
                ws.Cell(r, 2).Value = "Geçici K07 doğrulaması"; ws.Cell(r, 3).Value = marker + r;
                ws.Cell(r, 4).Value = new DateTime(2026, 9, 29); ws.Cell(r, 5).Value = 123.45;
                ws.Cell(r, 6).Value = "Evet"; ws.Cell(r, 7).Value = note; ws.Cell(r, 9).Value = "TL";
            }
            using var buffer = new MemoryStream(); wb.SaveAs(buffer); return buffer.ToArray();
        }
        async Task<long> Upload(byte[] bytes, string name)
        {
            var parsed = CollectionInvoiceFileParser.Parse(bytes, name, "B");
            files.Add($"collection-invoice-B-{parsed.FileHash.ToLowerInvariant()}.xlsx");
            using var stream = new MemoryStream(bytes);
            var result = await service.UploadAsync(new FormFile(stream, 0, bytes.Length, "file", name), "B", actor, default);
            if (!result.IsSuccess) throw new InvalidOperationException(result.Message);
            if (!loads.Contains(result.Data)) loads.Add(result.Data);
            return result.Data;
        }
        try
        {
            var bytes = Workbook("Birinci yükleme"); var id = await Upload(bytes, "k07-check.xlsx");
            if (await Upload(bytes, "k07-check-renamed.xlsx") != id) throw new InvalidOperationException("Dosya hash tekrar kontrolü başarısız.");
            var detail = await service.GetAsync(id, default);
            if (!detail.IsSuccess || detail.Data!.Ready != 1 || detail.Data.Errors != 1)
            {
                Console.WriteLine(JsonSerializer.Serialize((await service.RowsAsync(id, 1, 25, null, default)).Data!.Items.Select(x => new { x.RowNumber, x.Status, x.Issue })));
                throw new InvalidOperationException("Hazır/hatalı satır ayrımı başarısız.");
            }
            var rowPage = await service.RowsAsync(id, 1, 25, "Error", default);
            if (!rowPage.IsSuccess || rowPage.Data!.TotalCount != 1) throw new InvalidOperationException("Hata satırı sayfalaması başarısız.");
            if (!(await service.ListAsync(1, 25, default)).IsSuccess) throw new InvalidOperationException("Yükleme listesi başarısız.");
            var applied = await service.ProcessAsync(id, detail.Data.RowVersion, true, actor, default);
            if (!applied.IsSuccess) throw new InvalidOperationException(applied.Message);
            invoiceId = await db.Set<CollectionInvoiceLoadRow>().Where(x => x.LoadId == id && x.InvoiceId != null).Select(x => x.InvoiceId).SingleAsync();
            if (await db.Set<CollectionInvoicePayment>().AnyAsync(x => x.InvoiceId == invoiceId)) throw new InvalidOperationException("Ödendi mi alanı ödeme oluşturdu.");
            if ((await service.GetAsync(id, default)).Data!.Imported != 1) throw new InvalidOperationException("Aktarım sonucu yanlış.");
            if ((int)(await service.ProcessAsync(id, detail.Data.RowVersion, true, actor, default)).StatusCode != 409) throw new InvalidOperationException("Eski sürüm reddedilmedi.");
            var second = await Upload(Workbook("Farklı dosya aynı fatura"), "k07-other.xlsx");
            if ((await service.GetAsync(second, default)).Data!.Duplicates != 1) throw new InvalidOperationException("Farklı dosyada mükerrer kontrolü başarısız.");
            var inv = await new CollectionInvoiceService(db).GetAsync(invoiceId!.Value, default);
            var deleted = await new CollectionInvoiceCommandService(db).ExecuteAsync(invoiceId.Value, null, "InvoiceDelete", new CollectionInvoiceCommand { RequestId = request, InvoiceRowVersion = inv.Data!.RowVersion }, actor, default);
            if (!deleted.IsSuccess) throw new InvalidOperationException(deleted.Message);
            var beforeRefresh = (await service.GetAsync(second, default)).Data!;
            if (!(await service.ProcessAsync(second, beforeRefresh.RowVersion, false, actor, default)).IsSuccess || (await service.GetAsync(second, default)).Data!.Duplicates != 1)
                throw new InvalidOperationException("Silinen fatura yeniden yükleme koruması başarısız.");
            foreach (var file in files) if (!await storage.ExistsAsync(file)) throw new InvalidOperationException("CDN dosyası bulunamadı.");
            Console.WriteLine("K07 gerçek CDN/SQL kontrolü başarılı: yükleme, sayfalama, hata ayırma, aktarım, aynı/farklı dosya tekrar güvenliği ve silinmiş faturayı koruma. Ödeme oluşmadı.");
        }
        finally
        {
            db.ChangeTracker.Clear();
            // Only this run's uniquely named fixtures may be removed.
            await using var cleanup = await db.Database.BeginTransactionAsync();
            var fixtureIds = await db.Set<CollectionInvoice>().Where(x => x.Number == marker + "2" && x.LegacyInvoiceFollowId == null).Select(x => x.Id).ToListAsync();
            await db.Set<CollectionInvoicePayment>().Where(x => fixtureIds.Contains(x.InvoiceId)).ExecuteDeleteAsync();
            await db.Set<CollectionInvoice>().Where(x => fixtureIds.Contains(x.Id)).ExecuteDeleteAsync();
            await db.Set<CollectionInvoiceOperation>().Where(x => x.RequestId == request).ExecuteDeleteAsync();
            var testLoads = await db.Set<CollectionInvoiceLoad>().Where(x => files.Contains(x.StoredFileName)).Select(x => x.Id).ToListAsync();
            await db.Set<CollectionInvoiceLoadRow>().Where(x => testLoads.Contains(x.LoadId)).ExecuteDeleteAsync();
            await db.Set<CollectionInvoiceLoad>().Where(x => testLoads.Contains(x.Id)).ExecuteDeleteAsync();
            await db.Set<CollectionInvoiceAccount>().Where(x => x.Code == marker).ExecuteDeleteAsync();
            await cleanup.CommitAsync();
            foreach (var file in files) await storage.DeleteAsync(file);
            Console.WriteLine("Yalnız bu koşuya ait geçici K07 DB/CDN kayıtları temizlendi.");
        }
    }
}
