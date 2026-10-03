using System.Text.Json;
using Amazon.Runtime;
using Amazon.S3;
using Business.Services.Crm.Collections;
using Business.Services.Storage;
using ClosedXML.Excel;
using Core.Settings.Concrete;
using Data.Concrete.EfCore.Collections;
using Data.Concrete.EfCore.Context;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Model.Concrete;
using Model.Concrete.Collections;

internal static class CollectionBankLoadCheck
{
    public static async Task RunAsync(string path)
    {
        using var cfg = JsonDocument.Parse(await File.ReadAllTextAsync(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var cs = new SqlConnectionStringBuilder(cfg.RootElement.GetProperty("AppSettings").GetProperty("MSSQLConnectionString").GetString());
        if (cs.DataSource != "192.168.1.8" || cs.InitialCatalog != "AssistFlowTest") throw new InvalidOperationException("Yalnız test ortamında çalışır.");
        var opt = cfg.RootElement.GetProperty("R2Storage").Deserialize<R2StorageOptions>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        using var client = new AmazonS3Client(new BasicAWSCredentials(opt.AccessKeyId, opt.SecretAccessKey), new AmazonS3Config { ServiceURL = opt.Endpoint, ForcePathStyle = true });
        var storage = new R2FileStorage(client, Options.Create(opt), NullLogger<R2FileStorage>.Instance);
        await using var db = new AppDataContext(new DbContextOptionsBuilder<AppDataContext>().UseSqlServer(cs.ConnectionString, o => o.CommandTimeout(120)).Options);
        if ((await db.Database.GetPendingMigrationsAsync()).Any() || db.Database.HasPendingModelChanges()) throw new InvalidOperationException("Şema hazır değil.");
        var actor = await db.Users.Where(x => !x.IsDeleted).Select(x => x.Id).FirstAsync();
        var template = await CollectionCustomerScopeQuery.Contracts(db.Set<CollectionContract>().AsNoTracking(), db.Customers)
            .Where(x => x.Customer.SubscriberCompany != null && x.Customer.SubscriberCode != null &&
                EF.Functions.Like(x.Customer.SubscriberCode, "%[0-9][0-9][0-9][0-9]"))
            .Include(x => x.Customer).FirstAsync();
        var currency = await db.Set<CurrencyType>().Where(x => x.Code == "TRY" || x.Code == "TL").Select(x => x.Id).FirstAsync();
        var frequency = await db.Set<CollectionPaymentFrequency>().Select(x => x.Id).FirstAsync();
        var marker = "K08-" + Guid.NewGuid().ToString("N"); var period = new DateOnly(2026, 9, 1);
        var contract = new CollectionContract { CustomerId = template.CustomerId, ServiceTypeId = template.ServiceTypeId, StartDate = period,
            GtsNo = marker, IvrNo = marker, CreatedDate = DateTimeOffset.UtcNow, CreatedUser = actor };
        var secondContract = new CollectionContract { CustomerId = template.CustomerId, ServiceTypeId = template.ServiceTypeId, StartDate = period,
            GtsNo = marker + "-selection", CreatedDate = DateTimeOffset.UtcNow, CreatedUser = actor };
        var files = new HashSet<string>(); var keys = new HashSet<string>();
        var service = new CollectionBankLoadService(db, storage);
        byte[] Workbook(string type, string note)
        {
            using var wb = new XLWorkbook(); var ws = wb.AddWorksheet("Banka");
            string[] h = type == "GTS" ? ["RRN", "Kayıt No", "İşlem Tarihi", "İşlem Tipi", "Tutar", "Kur", "Dönüş Kodu", "Kredi Kart No"]
                : ["İşlem Numarası", "Sipariş Numarası", "İşlem Tarihi", "İşlem Tipi", "Sipariş Tutarı", "Döviz cinsi", "İşlem Durumu", "Kredi Kart No"];
            for (var c = 0; c < h.Length; c++) ws.Cell(1, c + 1).Value = h[c];
            for (var r = 2; r <= 5; r++)
            {
                var number = marker + r; keys.Add(CollectionBankLoadService.Key(type, number));
                ws.Cell(r, 1).Value = number; ws.Cell(r, 2).Value = r == 5 ? marker + "-missing" : marker;
                ws.Cell(r, 3).Value = new DateTime(2026, 9, 29); ws.Cell(r, 4).Value = r == 4 ? "İade" : type == "GTS" ? "Mail Order" : "Satış";
                ws.Cell(r, 5).Value = 12.34; ws.Cell(r, 6).Value = "TL";
                ws.Cell(r, 7).Value = r == 3 ? (type == "GTS" ? "05" : "Başarısız") : type == "GTS" ? "00" : "Başarılı";
                ws.Cell(r, 8).Value = "SENSITIVE-TEST-" + note;
            }
            using var ms = new MemoryStream(); wb.SaveAs(ms); return ms.ToArray();
        }
        async Task<long> Upload(byte[] bytes, string type)
        {
            var p = CollectionBankFileParser.Parse(bytes, "check.xlsx", type, period);
            files.Add($"collection-bank-{type}-{p.FileHash.ToLowerInvariant()}-{period:yyyyMM}.json");
            if (JsonSerializer.Serialize(p).Contains("SENSITIVE-TEST")) throw new Exception("Kart alanı sızdı.");
            using var ms = new MemoryStream(bytes);
            var result = await service.UploadAsync(new FormFile(ms, 0, bytes.Length, "file", "check.xlsx"), type, period, actor, default);
            if (!result.IsSuccess) throw new Exception(result.Message);
            return result.Data;
        }
        try
        {
            db.Add(contract); await db.SaveChangesAsync();
            db.Add(new CollectionContractRatePeriod { ContractId = contract.Id, EffectiveFrom = period, BillingAnchor = period,
                PaymentFrequencyId = frequency, Amount = 12.34m, CurrencyTypeId = currency, BillingBehavior = CollectionBillingBehavior.Billable, CreatedUser = actor, CreatedDate = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            foreach (var type in new[] { "GTS", "IVR" })
            {
                var bytes = Workbook(type, "first"); var id = await Upload(bytes, type);
                if (await Upload(bytes, type) != id) throw new Exception("Dosya tekrar kontrolü başarısız.");
                var d = (await service.GetAsync(id, default)).Data!;
                if (d.Ready != 1 || d.BankFailed != 1 || d.Review != 2)
                { Console.WriteLine(JsonSerializer.Serialize((await service.RowsAsync(id, 1, 25, null, default)).Data)); throw new Exception("Önizleme ayrımı başarısız."); }
                await using var concurrentDb = new AppDataContext(new DbContextOptionsBuilder<AppDataContext>().UseSqlServer(cs.ConnectionString, o => o.CommandTimeout(120)).Options);
                var concurrent = new CollectionBankLoadService(concurrentDb, storage);
                var applied = await Task.WhenAll(service.ProcessAsync(id, Convert.ToBase64String(d.RowVersion), true, actor, default),
                    concurrent.ProcessAsync(id, Convert.ToBase64String(d.RowVersion), true, actor, default));
                if (!applied.Any(x => x.IsSuccess)) throw new Exception(string.Join("; ", applied.Select(x => x.Message)));
                if (await db.Set<CollectionPayment>().CountAsync(x => x.ContractId == contract.Id) != 1) throw new Exception("Eşzamanlı aktarım çift ödeme oluşturdu veya ödeme eksik.");
                if ((await service.GetAsync(id, default)).Data!.Imported != 1) throw new Exception("Kuyruk/ödeme mutabakatı başarısız.");
                var second = await Upload(Workbook(type, "second"), type);
                if ((await service.GetAsync(second, default)).Data!.Duplicate != 1) throw new Exception("Farklı dosyada tekrar kontrolü başarısız.");
                var oldVersion = (await service.GetAsync(second, default)).Data!.RowVersion;
                await service.ProcessAsync(second, Convert.ToBase64String(oldVersion), false, actor, default);
                if ((int)(await service.ProcessAsync(second, Convert.ToBase64String(oldVersion), true, actor, default)).StatusCode != 409) throw new Exception("Eski önizleme reddedilmedi.");
                // Only fixture payments: financial deletion must not release the bank identity.
                await db.Set<CollectionPayment>().Where(x => x.ContractId == contract.Id).ExecuteDeleteAsync();
                var current = (await service.GetAsync(second, default)).Data!;
                await service.ProcessAsync(second, Convert.ToBase64String(current.RowVersion), false, actor, default);
                if ((await service.GetAsync(second, default)).Data!.Duplicate != 1) throw new Exception("Silinmiş ödemenin banka kimliği kayboldu.");
            }
            // A single payment must remain unassigned until the user selects one of this customer's contracts.
            db.Add(secondContract); await db.SaveChangesAsync();
            db.Add(new CollectionContractRatePeriod { ContractId = secondContract.Id, EffectiveFrom = period, BillingAnchor = period,
                PaymentFrequencyId = frequency, Amount = 12.34m, CurrencyTypeId = currency, BillingBehavior = CollectionBillingBehavior.Billable, CreatedUser = actor, CreatedDate = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            using (var wb = new XLWorkbook())
            {
                var ws = wb.AddWorksheet("Müşteri ödemeleri");
                string[] h = ["RRN", "Kayıt No", "İşlem Tarihi", "İşlem Tipi", "Tutar", "Kur", "Dönüş Kodu", "ABONE NO", "Müşteri Adı", "Net Tutar", "Kur"];
                for (var c = 0; c < h.Length; c++) ws.Cell(1, c + 1).Value = h[c];
                var number = marker + "-manual"; keys.Add(CollectionBankLoadService.Key("GTS", number));
                ws.Cell(2, 1).Value = number; ws.Cell(2, 2).Value = marker;
                ws.Cell(2, 3).Value = "2026-09-30 14:46:08.319";
                ws.Cell(2, 4).Value = "Mail Order"; ws.Cell(2, 5).Value = 12.34;
                ws.Cell(2, 6).Value = "TL"; ws.Cell(2, 7).Value = "00";
                ws.Cell(2, 8).Value = template.Customer.SubscriberCode![^4..]; ws.Cell(2, 9).Value = template.Customer.SubscriberCompany!;
                ws.Cell(2, 10).Value = 999; ws.Cell(2, 11).Value = 0;
                using var stream = new MemoryStream(); wb.SaveAs(stream);
                var id = await Upload(stream.ToArray(), "GTS");
                var row = (await service.RowsAsync(id, 1, 25, null, default)).Data!.Items.Single();
                if (row.Status != "Review" || row.ContractId.HasValue || row.Source.Amount != 12.34m) throw new Exception("Birden çok sözleşme otomatik seçildi veya Net Tutar kullanıldı.");
                var available = await service.ContractsAsync(id, row.Id, 1, 100, default);
                if (!available.IsSuccess || !available.Data!.Items.Any(x => x.Id == secondContract.Id)) throw new Exception("Sözleşme seçenekleri bulunamadı.");
                var version = Convert.ToBase64String((await service.GetAsync(id, default)).Data!.RowVersion);
                if ((await service.SelectContractAsync(id, row.Id, new(version, long.MaxValue), actor, default)).IsSuccess) throw new Exception("Başka müşteriye sözleşme seçilebildi.");
                var selection = await service.SelectContractAsync(id, row.Id, new(version, secondContract.Id), actor, default);
                if (!selection.IsSuccess) throw new Exception(selection.Message);
                if ((int)(await service.SelectContractAsync(id, row.Id, new(version, contract.Id), actor, default)).StatusCode != 409) throw new Exception("Eski seçim sürümü kabul edildi.");
                row = (await service.RowsAsync(id, 1, 25, null, default)).Data!.Items.Single();
                if (row.Status != "Ready" || row.Source.Selections?.Single().UserId != actor) throw new Exception("Seçim veya kullanıcı izi korunmadı.");
                version = Convert.ToBase64String((await service.GetAsync(id, default)).Data!.RowVersion);
                var applied = await service.ProcessAsync(id, version, true, actor, default);
                if (!applied.IsSuccess) throw new Exception(applied.Message);
                if (!await db.Set<CollectionPayment>().AnyAsync(x => x.ContractId == secondContract.Id && x.Amount == 12.34m && x.Period == period)) throw new Exception("Seçilen sözleşmeye ödeme oluşmadı.");
                version = Convert.ToBase64String((await service.GetAsync(id, default)).Data!.RowVersion);
                if ((await service.SelectContractAsync(id, row.Id, new(version, contract.Id), actor, default)).IsSuccess) throw new Exception("Aktarılmış satırda seçim değiştirildi.");
            }
            if (!(await service.ListAsync(1, 25, default)).IsSuccess) throw new Exception("Liste sorgusu başarısız.");
            foreach (var file in files) if (!await storage.ExistsAsync(file)) throw new Exception("CDN kaydı bulunamadı.");
            Console.WriteLine("K08 gerçek SQL/CDN kontrolü başarılı: GTS/IVR, önizleme, başarısız/iade/eşleşmeyen ayrımı, ödeme+audit+kuyruk, tekrar/silinme/sürüm koruması.");
        }
        finally
        {
            db.ChangeTracker.Clear(); await using var tx = await db.Database.BeginTransactionAsync();
            var requests = keys.Select(x => new Guid(Convert.FromHexString(x)[..16])).ToArray();
            await db.Set<CollectionPaymentOperation>().Where(x => requests.Contains(x.RequestId)).ExecuteDeleteAsync();
            await db.Set<CollectionPayment>().Where(x => x.ContractId == contract.Id && contract.Id != 0).ExecuteDeleteAsync();
            await db.Set<CollectionPayment>().Where(x => x.ContractId == secondContract.Id && secondContract.Id != 0).ExecuteDeleteAsync();
            await db.Set<CollectionBankTransaction>().Where(x => keys.Contains(x.Key) && x.Source == "AssistFlow").ExecuteDeleteAsync();
            var loads = await db.Set<CollectionBankLoad>().Where(x => files.Contains(x.StoredFileName)).Select(x => x.Id).ToListAsync();
            await db.Set<CollectionBankLoadRow>().Where(x => loads.Contains(x.LoadId)).ExecuteDeleteAsync();
            await db.Set<CollectionBankLoad>().Where(x => loads.Contains(x.Id)).ExecuteDeleteAsync();
            await db.Set<CollectionContractRatePeriod>().Where(x => x.ContractId == contract.Id && contract.Id != 0).ExecuteDeleteAsync();
            await db.Set<CollectionContractRatePeriod>().Where(x => x.ContractId == secondContract.Id && secondContract.Id != 0).ExecuteDeleteAsync();
            await db.Set<CollectionContract>().Where(x => x.Id == secondContract.Id && x.GtsNo == marker + "-selection").ExecuteDeleteAsync();
            await db.Set<CollectionContract>().Where(x => x.Id == contract.Id && x.GtsNo == marker).ExecuteDeleteAsync();
            await tx.CommitAsync();
            foreach (var file in files) await storage.DeleteAsync(file);
            Console.WriteLine("Yalnız bu koşunun geçici K08 sözleşme/ödeme/DB/CDN kayıtları temizlendi.");
        }
    }
}
