using System.Text;
using System.Text.Json;
using Amazon.Runtime;
using Amazon.S3;
using Business.Services.Crm.Collections;
using Business.Services.Crm.Collections.Calculation;
using Business.Services.Storage;
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
using Model.Dtos.Crm.Collections;

internal static class CollectionK09Check
{
    public static async Task RunAsync(string path, bool install)
    {
        using var cfg = JsonDocument.Parse(await File.ReadAllTextAsync(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var cs = new SqlConnectionStringBuilder(cfg.RootElement.GetProperty("AppSettings").GetProperty("MSSQLConnectionString").GetString());
        if (cs.DataSource != "192.168.1.8" || cs.InitialCatalog != "AssistFlowTest") throw new InvalidOperationException("Yalnız AssistFlowTest üzerinde çalışır.");
        await using var db = new AppDataContext(new DbContextOptionsBuilder<AppDataContext>().UseSqlServer(cs.ConnectionString).Options);
        if (db.Database.HasPendingModelChanges()) throw new Exception("Model/snapshot farklı.");
        if (install)
        {
            if ((await db.Database.GetPendingMigrationsAsync()).Any(x => !x.EndsWith("_AddCollectionContractCorrections") && !x.EndsWith("_AddCollectionCustomerFileActions") && !x.EndsWith("_EnableCollectionRequestedPaymentMethods"))) throw new Exception("Yalnız K09 migrationı uygulanabilir.");
            await db.Database.MigrateAsync(); Console.WriteLine("K09 test şeması hazır."); return;
        }
        if ((await db.Database.GetPendingMigrationsAsync()).Any()) throw new Exception("Bekleyen migration var.");
        var opt = cfg.RootElement.GetProperty("R2Storage").Deserialize<R2StorageOptions>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        using var client = new AmazonS3Client(new BasicAWSCredentials(opt.AccessKeyId, opt.SecretAccessKey), new AmazonS3Config { ServiceURL = opt.Endpoint, ForcePathStyle = true });
        var storage = new R2FileStorage(client, Options.Create(opt), NullLogger<R2FileStorage>.Instance);
        var actor = await db.Users.Where(x => !x.IsDeleted).Select(x => x.Id).FirstAsync();
        var template = await CollectionCustomerScopeQuery.Contracts(db.Set<CollectionContract>().AsNoTracking(), db.Customers).FirstAsync();
        var currency = await db.Set<CurrencyType>().Where(x => x.Code == "TRY" || x.Code == "TL").Select(x => x.Id).FirstAsync();
        var frequency = await db.Set<CollectionPaymentFrequency>().Where(x => x.IsActive && x.IntervalMonths == 1).Select(x => x.Id).FirstAsync();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "Europe/Istanbul").DateTime);
        var period = new DateOnly(today.Year, today.Month, 1); var marker = "K09-" + Guid.NewGuid().ToString("N");
        var contracts = Enumerable.Range(0, 4).Select(i => new CollectionContract { CreationRequestId = Guid.NewGuid(), CreationPayloadHash = new byte[32], CustomerId = template.CustomerId, ServiceTypeId = template.ServiceTypeId,
            StartDate = period, GtsNo = marker + i, CreatedUser = actor, CreatedDate = DateTimeOffset.UtcNow }).ToArray();
        var requests = new List<Guid>(); var stored = new List<string>();
        var fileRequest = Guid.NewGuid(); var manualFilePath = $"manual/{template.CustomerId}/{fileRequest:N}";
        var correction = new CollectionContractCorrectionService(db); var payments = new CollectionPaymentService(db);
        var attachments = new CollectionContractAttachmentService(db, storage, NullLogger<CollectionContractAttachmentService>.Instance);
        async Task<string> Version(long id) => Convert.ToBase64String(await db.Set<CollectionContract>().Where(x => x.Id == id).Select(x => x.RowVersion).SingleAsync());
        try
        {
            db.AddRange(contracts); await db.SaveChangesAsync();
            foreach (var c in contracts) db.Add(new CollectionContractRatePeriod { ContractId = c.Id, EffectiveFrom = period, BillingAnchor = period,
                Amount = 100, CurrencyTypeId = currency, PaymentFrequencyId = frequency, BillingBehavior = CollectionBillingBehavior.Billable, CreatedUser = actor, CreatedDate = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var annual = contracts[3];
            var activeId = await db.Set<CollectionSubscriptionStatus>().Where(x => x.Code == "ACTIVE" && x.IsActive).Select(x => x.Id).FirstAsync();
            var existsId = await db.Set<CollectionContractStatus>().Where(x => x.Code == "EXISTS" && x.IsActive).Select(x => x.Id).FirstAsync();
            await db.Set<CollectionContract>().Where(x => x.Id == annual.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.SubscriptionStatusId, activeId).SetProperty(x => x.ContractStatusId, existsId));
            var annualCommand = new CollectionRateChange { Amount = 125, Reason = "K09 yıllık zam kontrolü", AnniversaryYear = today.Year + 1,
                RowVersion = Convert.FromBase64String(await Version(annual.Id)) };
            var annualService = new CollectionRateChangeService(db);
            var annualResult = await annualService.ChangeAsync(annual.Id, annualCommand, actor);
            if (!annualResult.IsSuccess || annualResult.Data!.EffectiveFrom != period.AddYears(1)) throw new Exception("Yıllık zam tarihi: " + annualResult.Message);
            if ((await annualService.ChangeAsync(annual.Id, annualCommand, actor)).IsSuccess) throw new Exception("Aynı yıllık zam tekrar uygulandı.");
            var annualRates = await db.Set<CollectionContractRatePeriod>().AsNoTracking().Where(x => x.ContractId == annual.Id).OrderBy(x => x.EffectiveFrom).ToListAsync();
            if (annualRates.Count != 2 || annualRates[0].Amount != 100 || annualRates[0].EffectiveToExclusive != period.AddYears(1) || annualRates[1].Amount != 125)
                throw new Exception("Yıllık zam eski tarife koruması başarısız.");
            var tracking = new CollectionTrackingService(db);
            foreach (var view in new[] { CollectionFollowUpView.Individual, CollectionFollowUpView.Group })
            {
                var tq = new CollectionTrackingQuery { Period = period, Search = marker, View = view, PageSize = 100 };
                var page = await tracking.GetPageAsync(tq);
                var totals = await tracking.GetTotalsAsync(tq);
                if (!totals.IsSuccess || !page.IsSuccess || totals.Data!.Sum(x => x.AccruedAmount) != page.Data!.Items.Sum(x => x.AccruedAmount))
                    throw new Exception("Takip toplamlarının SQL/filtre mutabakatı başarısız.");
            }
            Console.WriteLine("Yıllık zam ileri tarih/tekrar/eski tarife ve bireysel/grup SQL toplamları doğrulandı.");
            var source = contracts[0].Id; var target = contracts[1].Id;
            var command = new CollectionContractCorrectionCommand(Guid.NewGuid(), await Version(source), "Terms", "K09 geçici kontrol", true, period,
                period.AddYears(1), null, null, null, null, null, null);
            if (!(await correction.ExecuteAsync(source, command, actor, default)).IsSuccess || !(await correction.ExecuteAsync(source, command, actor, default)).IsSuccess) throw new Exception("Tarih düzeltmesi/tekrar başarısız.");
            if ((int)(await correction.ExecuteAsync(source, command with { RequestId = Guid.NewGuid() }, actor, default)).StatusCode != 409) throw new Exception("Eski sürüm kabul edildi.");
            var rate = await db.Set<CollectionContractRatePeriod>().AsNoTracking().SingleAsync(x => x.ContractId == target);
            var rateCommand = new CollectionContractCorrectionCommand(Guid.NewGuid(), await Version(target), "Rate", "K09 tarife kontrol", true,
                null, null, null, rate.Id, Convert.ToBase64String(rate.RowVersion), 120, currency, frequency);
            if (!(await correction.ExecuteAsync(target, rateCommand, actor, default)).IsSuccess) throw new Exception("Tarife düzeltmesi başarısız.");
            var create = Guid.NewGuid(); requests.Add(create);
            var made = await payments.CreateAsync(source, new() { RequestId = create, Period = period, PaymentDate = period, Amount = 12.34m, CurrencyTypeId = currency, Description = marker }, actor);
            if (!made.IsSuccess) throw new Exception(made.Message);
            var payment = await db.Set<CollectionPayment>().AsNoTracking().SingleAsync(x => x.Id == made.Data!.PaymentId);
            var move = Guid.NewGuid(); requests.Add(move);
            var change = new CollectionPaymentMove { TargetContractId = target, Reason = "K09 yanlış sözleşme kontrolü", Payment = new() { RequestId = move,
                RowVersion = Convert.ToBase64String(payment.RowVersion), Period = payment.Period, PaymentDate = payment.PaymentDate, Amount = payment.Amount, CurrencyTypeId = payment.CurrencyTypeId, Description = payment.Description } };
            if (!(await payments.MoveAsync(source, payment.Id, change, actor)).IsSuccess || !(await payments.MoveAsync(source, payment.Id, change, actor)).IsSuccess) throw new Exception("Ödeme taşıma/tekrar başarısız.");
            if (!await db.Set<CollectionPayment>().AnyAsync(x => x.Id == payment.Id && x.ContractId == target && x.Amount == 12.34m)) throw new Exception("Ödeme kimliği/tutarı korunmadı.");
            rate = await db.Set<CollectionContractRatePeriod>().AsNoTracking().SingleAsync(x => x.ContractId == target);
            if ((await correction.ExecuteAsync(target, rateCommand with { RequestId = Guid.NewGuid(), RowVersion = await Version(target), RateRowVersion = Convert.ToBase64String(rate.RowVersion) }, actor, default)).IsSuccess) throw new Exception("Ödemeli tarife düzeltmesi engellenmedi.");
            var bytes = Encoding.ASCII.GetBytes("%PDF-1.4\n% K09 temporary signature fixture\n%%EOF"); using var stream = new MemoryStream(bytes);
            var upload = await attachments.UploadAsync(source, new FormFile(stream, 0, bytes.Length, "file", marker + ".pdf") { Headers = new HeaderDictionary(), ContentType = "application/pdf" }, actor);
            if (!upload.IsSuccess) throw new Exception(upload.Message);
            var attachment = await db.Set<CollectionContractAttachment>().AsNoTracking().SingleAsync(x => x.Id == upload.Data!.Id); stored.Add(attachment.StoredFileName); db.ChangeTracker.Clear();
            if ((await attachments.RemoveAsync(target, attachment.Id, actor)).IsSuccess) throw new Exception("Başka sözleşmenin dosyası kaldırıldı.");
            if (!(await attachments.RemoveAsync(source, attachment.Id, actor)).IsSuccess) throw new Exception("Dosya kaldırılamadı.");
            var removedAt = await db.Set<CollectionContractAttachment>().Where(x => x.Id == attachment.Id).Select(x => x.UpdatedDate).SingleAsync();
            await attachments.RemoveAsync(source, attachment.Id, actor);
            if (!await storage.ExistsAsync(attachment.StoredFileName) || (await attachments.GetPageAsync(source, 1, 25)).Data!.TotalCount != 0 ||
                removedAt != await db.Set<CollectionContractAttachment>().Where(x => x.Id == attachment.Id).Select(x => x.UpdatedDate).SingleAsync()) throw new Exception("Arşiv/liste/ilk işlem tarihi koruması başarısız.");
            if ((await correction.HistoryAsync(source, 1, 25, default)).Data!.TotalCount != 1) throw new Exception("Audit tekrarlandı.");
            var dated = contracts[2];
            var datedRate = await db.Set<CollectionContractRatePeriod>().AsNoTracking().SingleAsync(x => x.ContractId == dated.Id);
            var datedCommand = rateCommand with { RequestId = Guid.NewGuid(), RowVersion = await Version(dated.Id), RateId = datedRate.Id,
                RateRowVersion = Convert.ToBase64String(datedRate.RowVersion), CorrectRateDates = true, RateEffectiveFrom = period.AddDays(1), RateEffectiveToExclusive = period.AddMonths(1) };
            if (!(await correction.ExecuteAsync(dated.Id, datedCommand, actor, default)).IsSuccess) throw new Exception("Tarife tarih düzeltmesi başarısız.");
            db.Add(new CollectionContractRatePeriod { ContractId = dated.Id, EffectiveFrom = period.AddMonths(1), BillingAnchor = period,
                Amount = 100, CurrencyTypeId = currency, PaymentFrequencyId = frequency, BillingBehavior = CollectionBillingBehavior.Billable, CreatedUser = actor, CreatedDate = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            datedRate = await db.Set<CollectionContractRatePeriod>().AsNoTracking().SingleAsync(x => x.Id == datedRate.Id);
            if ((await correction.ExecuteAsync(dated.Id, datedCommand with { RequestId = Guid.NewGuid(), RowVersion = await Version(dated.Id),
                RateRowVersion = Convert.ToBase64String(datedRate.RowVersion), RateEffectiveToExclusive = period.AddMonths(1).AddDays(1) }, actor, default)).IsSuccess)
                throw new Exception("Çakışan tarife kabul edildi.");
            var delete = command with { RequestId = Guid.NewGuid(), Kind = "Delete", RowVersion = await Version(source) };
            if ((await correction.ExecuteAsync(source, delete, actor, default)).IsSuccess) throw new Exception("Dosya bağlantılı sözleşme silindi.");
            if ((await correction.ExecuteAsync(target, delete with { RequestId = Guid.NewGuid(), RowVersion = await Version(target) }, actor, default)).IsSuccess) throw new Exception("Ödemeli sözleşme silindi.");
            var manual = contracts[2]; delete = delete with { RequestId = Guid.NewGuid(), RowVersion = await Version(manual.Id) };
            if (!(await correction.ExecuteAsync(manual.Id, delete, actor, default)).IsSuccess || !(await correction.ExecuteAsync(manual.Id, delete, actor, default)).IsSuccess) throw new Exception("Bağımsız sözleşme silme/tekrar başarısız.");
            if (await db.Set<CollectionContract>().AnyAsync(x => x.Id == manual.Id) || await db.Set<CollectionContractRatePeriod>().AnyAsync(x => x.ContractId == manual.Id) ||
                (await correction.HistoryAsync(manual.Id, 1, 25, default)).Data!.TotalCount != 2) throw new Exception("Fiziksel silme/kalıcı audit başarısız.");
            var recreate = await new CollectionContractCreateService(db).CreateAsync(new() { RequestId = manual.CreationRequestId!.Value, CustomerId = manual.CustomerId,
                ServiceTypeId = manual.ServiceTypeId, StartDate = period, SubscriptionStatusId = 1, PaymentFrequencyId = frequency, CurrencyTypeId = currency, Amount = 100 }, actor);
            if ((int)recreate.StatusCode != 409) throw new Exception("Silinen oluşturma isteği yeniden kabul edildi.");
            var oldTimeline = await db.Set<CollectionContractRatePeriod>().AsNoTracking().Where(x => x.ContractId == target).ToListAsync();
            var oldCharges = CollectionAccrualRules.Calculate(oldTimeline.Select(x => CollectionRateSegmentMapper.FromPersisted(x, 1)), period, today.AddDays(1));
            var stop = command with { RequestId = Guid.NewGuid(), Kind = "Stop", RowVersion = await Version(target) };
            if (!(await correction.ExecuteAsync(target, stop, actor, default)).IsSuccess || !(await correction.ExecuteAsync(target, stop, actor, default)).IsSuccess)
                throw new Exception("YOK geçişi/tekrarı başarısız.");
            var timeline = await db.Set<CollectionContractRatePeriod>().AsNoTracking().Where(x => x.ContractId == target).ToListAsync();
            var newCharges = CollectionAccrualRules.Calculate(timeline.Select(x => CollectionRateSegmentMapper.FromPersisted(x, 1)), period, today.AddMonths(3));
            if (!oldCharges.Select(x => (x.DueDate, x.Amount, x.CurrencyTypeId)).SequenceEqual(newCharges.Select(x => (x.DueDate, x.Amount, x.CurrencyTypeId))) ||
                !await db.Set<CollectionPayment>().AnyAsync(x => x.Id == payment.Id && x.ContractId == target && x.Amount == 12.34m) ||
                (await correction.HistoryAsync(target, 1, 25, default)).Data!.TotalCount != 2)
                throw new Exception("YOK geçişinde geçmiş borç/ödeme/audit korunmadı veya geleceğe borç üretildi.");
            Console.WriteLine("YOK geçişi doğrulandı: bugün dahil geçmiş borç/ödeme aynı, geleceğe borç yok; tekrar isteği tek işlem.");
            using var customerStream = new MemoryStream(bytes);
            var customerFile = new FormFile(customerStream, 0, bytes.Length, "file", marker + ".pdf") { Headers = new HeaderDictionary(), ContentType = "application/pdf" };
            if (!(await attachments.UploadCustomerAsync(template.CustomerId, customerFile, fileRequest, actor)).IsSuccess ||
                !(await attachments.UploadCustomerAsync(template.CustomerId, customerFile, fileRequest, actor)).IsSuccess)
                throw new Exception("Müşteri dosyası yükleme/tekrar kontrolü başarısız.");
            var uploaded = await db.Set<CollectionCustomerAttachment>().AsNoTracking().SingleAsync(x => x.SourcePath == manualFilePath);
            stored.Add(uploaded.StoredFileName);
            var otherCustomer = await CollectionCustomerScopeQuery.Customers(db.Customers.AsNoTracking()).Where(x => x.Id != template.CustomerId).Select(x => x.Id).FirstAsync();
            if ((await attachments.RemoveCustomerAsync(otherCustomer, uploaded.Id, actor)).IsSuccess) throw new Exception("Yanlış müşteride dosya kaldırma kabul edildi.");
            await attachments.RemoveCustomerAsync(template.CustomerId, uploaded.Id, actor);
            var removedTime = await db.Set<CollectionCustomerAttachment>().Where(x => x.Id == uploaded.Id).Select(x => x.RemovedDate).SingleAsync();
            await attachments.RemoveCustomerAsync(template.CustomerId, uploaded.Id, actor);
            await attachments.UploadCustomerAsync(template.CustomerId, customerFile, fileRequest, actor);
            if (!await db.Set<CollectionCustomerAttachment>().AnyAsync(x => x.Id == uploaded.Id && x.IsDeleted && x.RemovedDate == removedTime && x.RemovedUser == actor) ||
                !await storage.ExistsAsync(uploaded.StoredFileName) || (await attachments.GetCustomerPageAsync(template.CustomerId, 1, 100)).Data!.Items.Any(x => x.Id == uploaded.Id))
                throw new Exception("Kaldırma audit/arşiv/yeniden etkinleşmeme koruması başarısız.");
            Console.WriteLine("Müşteri dosyası doğrulandı: yükleme, tekil tekrar, sahiplik, kaldırma audit'i, CDN arşivi ve yeniden etkinleşmeme.");
            Console.WriteLine("K09 test SQL/CDN kontrolü başarılı: tarih/tarife, sürüm/tekrar/audit, ödeme taşıma, ödemeli dönem engeli, dosya/arşiv, kontrollü fiziksel silme ve eski oluşturma isteği koruması.");
        }
        finally
        {
            db.ChangeTracker.Clear(); var ids = contracts.Where(x => x.Id > 0).Select(x => x.Id).ToArray();
            stored.AddRange(await db.Set<CollectionContractAttachment>().Where(x => ids.Contains(x.ContractId) && x.OriginalFileName == marker + ".pdf").Select(x => x.StoredFileName).ToListAsync());
            stored.AddRange(await db.Set<CollectionCustomerAttachment>().Where(x => x.SourcePath == manualFilePath).Select(x => x.StoredFileName).ToListAsync());
            await using var tx = await db.Database.BeginTransactionAsync();
            await db.Set<CollectionCustomerAttachment>().Where(x => x.SourcePath == manualFilePath).ExecuteDeleteAsync();
            await db.Set<CollectionPaymentOperation>().Where(x => requests.Contains(x.RequestId)).ExecuteDeleteAsync();
            await db.Set<CollectionPayment>().Where(x => ids.Contains(x.ContractId)).ExecuteDeleteAsync();
            await db.Set<CollectionContractCorrection>().Where(x => ids.Contains(x.ContractId)).ExecuteDeleteAsync();
            await db.Set<CollectionContractAttachment>().Where(x => ids.Contains(x.ContractId)).ExecuteDeleteAsync();
            await db.Set<CollectionContractRatePeriod>().Where(x => ids.Contains(x.ContractId)).ExecuteDeleteAsync();
            await db.Set<CollectionContract>().Where(x => ids.Contains(x.Id) && x.GtsNo!.StartsWith(marker)).ExecuteDeleteAsync();
            await tx.CommitAsync(); foreach (var file in stored.Distinct()) await storage.DeleteAsync(file);
            Console.WriteLine("Yalnız bu koşunun geçici K09 kayıtları ve dosyası temizlendi.");
        }
    }
}
