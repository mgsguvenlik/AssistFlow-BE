using System.Text.Json;
using Business.Interfaces;
using Business.Services.Crm.Collections;
using Business.Services.Sms;
using Core.Settings.Concrete;
using Data.Concrete.EfCore.Collections;
using Data.Concrete.EfCore.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Model.Concrete.Collections;
using Model.Dtos.Crm.Collections;

internal static class CollectionSmsWorkflowCheck
{
    public static async Task RunAsync(string settingsPath)
    {
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath),
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var cs = new SqlConnectionStringBuilder(settings.RootElement.GetProperty("AppSettings").GetProperty("MSSQLConnectionString").GetString());
        if (cs.DataSource != "192.168.1.8" || cs.InitialCatalog != "AssistFlowTest") throw new InvalidOperationException("Yalnız AssistFlowTest kontrol edilebilir.");
        var dbOptions = new DbContextOptionsBuilder<AppDataContext>().UseSqlServer(cs.ConnectionString).Options;
        await using var db = new AppDataContext(dbOptions);
        if (db.Database.HasPendingModelChanges() || (await db.Database.GetPendingMigrationsAsync()).Any())
            throw new InvalidOperationException("SMS model veya migration kurulumu tamamlanmamış.");
        var candidates = await CollectionCustomerScopeQuery.Customers(db.Customers.AsNoTracking(), CollectionCustomerClass.Individual)
            .Where(x => x.SubscriberCode != null).Select(x => new { x.Id, x.Phone1, x.Phone2 }).Take(2000).ToListAsync();
        var customer = candidates.FirstOrDefault(x => SmsPhone.Select(x.Phone1, x.Phone2) is not null)
            ?? throw new InvalidOperationException("Salt-okunur müşteri listesinde geçerli telefonlu bireysel test referansı bulunamadı.");
        var parent = await db.Set<CollectionGroupParent>().AsNoTracking()
            .Where(x => db.Customers.Any(c => c.Id == x.CustomerId && !c.IsDeleted && c.CustomerType != null && c.CustomerType.Code == "G"))
            .Select(x => new { x.CustomerId, x.CustomerGroupId }).FirstOrDefaultAsync();
        var memberId = parent is null ? 0 : await CollectionCustomerScopeQuery.Customers(db.Customers.AsNoTracking(), CollectionCustomerClass.Group)
            .Where(x => x.CustomerGroupId == parent.CustomerGroupId && x.Id != parent.CustomerId).Select(x => x.Id).FirstOrDefaultAsync();
        var actor = await db.Users.Where(x => !x.IsDeleted).Select(x => x.Id).FirstAsync();
        var serviceType = await db.ServiceTypes.Select(x => x.Id).FirstAsync();
        var currency = await db.CurrencyTypes.Where(x => x.Code == "TRY" || x.Code == "TL").Select(x => x.Id).FirstAsync();
        var frequency = await db.Set<CollectionPaymentFrequency>().Where(x => x.IntervalMonths == 1 && x.IsActive).Select(x => x.Id).FirstAsync();
        var active = await db.Set<CollectionSubscriptionStatus>().Where(x => x.Code == "ACTIVE" && x.IsActive).Select(x => x.Id).FirstAsync();
        var exists = await db.Set<CollectionContractStatus>().Where(x => x.Code == "EXISTS" && x.IsActive).Select(x => x.Id).FirstAsync();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "Europe/Istanbul").DateTime);
        var start = today.AddYears(-2);
        var marker = "SMS-CHECK-" + Guid.NewGuid().ToString("N");
        var contracts = Enumerable.Range(0, memberId > 0 ? 5 : 4).Select(i => new CollectionContract
        {
            CreationRequestId = Guid.NewGuid(), CreationPayloadHash = new byte[32],
            CustomerId = i == 4 ? memberId : customer.Id, ServiceTypeId = serviceType, StartDate = start,
            SubscriptionStatusId = active, ContractStatusId = exists, GtsNo = marker + i,
            CreatedUser = actor, CreatedDate = DateTimeOffset.UtcNow
        }).ToArray();
        var sms = new CollectionSmsService(db, Options.Create(new SmsServiceOptions
            { CollectionSmsEnabled = true, SmsSimulationEnabled = true }), new TestEnvironment());
        var sender = new RejectSender();
        async Task<long> Change(int i, bool automatic)
        {
            db.ChangeTracker.Clear();
            var version = await db.Set<CollectionContract>().Where(x => x.Id == contracts[i].Id).Select(x => x.RowVersion).SingleAsync();
            var result = await new CollectionRateChangeService(db, automatic ? sms : null).ChangeAsync(contracts[i].Id,
                new CollectionRateChange { Amount = 1250, Reason = "SMS kontrollü zam", AnniversaryYear = today.Year + 1, RowVersion = version }, actor);
            if (!result.IsSuccess) throw new InvalidOperationException("Zam kontrolü başarısız: " + result.Message);
            return await db.Set<CollectionContractRatePeriod>().Where(x => x.ContractId == contracts[i].Id)
                .OrderByDescending(x => x.EffectiveFrom).Select(x => x.Id).FirstAsync();
        }
        async Task<CollectionSmsNotification> Notice(int i) => await db.Set<CollectionSmsNotification>().AsNoTracking().SingleAsync(x => x.ContractId == contracts[i].Id);
        async Task Process(long id)
        {
            await using var worker = new AppDataContext(dbOptions);
            await CollectionSmsDispatcher.ProcessNextAsync(worker, sender, onlyNotificationId: id);
        }
        async Task<CollectionSmsPreview> Preview(int i)
        {
            db.ChangeTracker.Clear(); var p = await sms.PreviewAsync(contracts[i].Id, default);
            if (!p.IsSuccess) throw new InvalidOperationException(p.Message);
            return p.Data!;
        }
        try
        {
            db.AddRange(contracts); await db.SaveChangesAsync();
            for (var i = 0; i < contracts.Length; i++) db.Add(new CollectionContractRatePeriod
            {
                ContractId = contracts[i].Id, EffectiveFrom = start, BillingAnchor = start,
                Amount = i == 2 ? 0 : 1000, CurrencyTypeId = currency, PaymentFrequencyId = frequency,
                BillingBehavior = CollectionBillingBehavior.Billable, CreatedUser = actor, CreatedDate = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            await Change(0, true);
            var automatic = await Notice(0);
            if (automatic.Status != CollectionSmsStatus.Pending || automatic.IncreasePercent != 25 || automatic.EffectiveDate != today.AddYears(1))
                throw new InvalidOperationException("Atomik zam/kuyruk kontrolü başarısız.");
            // Concurrent workers must claim this notification only once. Simulation never invokes sender.
            await Task.WhenAll(Process(automatic.Id), Process(automatic.Id));
            if ((await Notice(0)).Status != CollectionSmsStatus.Simulation || sender.Calls != 0 || (await Preview(0)).CanSend)
                throw new InvalidOperationException("Simülasyon, eşzamanlı claim veya mükerrer kontrolü başarısız.");
            await Change(1, false);
            var manual = await Preview(1);
            var cmd = new CollectionSmsSendCommand { RatePeriodId = manual.RatePeriodId, PreviewHash = manual.PreviewHash, Reason = "Manuel deneme" };
            if (!(await sms.SendAsync(contracts[1].Id, cmd, actor, default)).IsSuccess
                || (await sms.SendAsync(contracts[1].Id, cmd, actor, default)).IsSuccess)
                throw new InvalidOperationException("Manuel kuyruk/mükerrer kontrolü başarısız.");
            var manualNotice = await Notice(1);
            // Only this fixture is switched to fake rejection; no real provider exists in this check.
            await db.Set<CollectionSmsAttempt>().Where(x => x.NotificationId == manualNotice.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsSimulation, false));
            await Process(manualNotice.Id);
            if ((await Notice(1)).Status != CollectionSmsStatus.Failed || sender.Calls != 1)
                throw new InvalidOperationException("Hatalı gönderim kontrolü başarısız.");
            manual = await Preview(1);
            cmd.PreviewHash = manual.PreviewHash; cmd.Reason = "Hata sonrası kontrollü yeniden deneme";
            if (!manual.CanSend || !(await sms.SendAsync(contracts[1].Id, cmd, actor, default)).IsSuccess)
                throw new InvalidOperationException("Kontrollü yeniden deneme başarısız.");
            await Process(manualNotice.Id);
            var history = await sms.HistoryAsync(contracts[1].Id, 1, 25, null, default);
            if (!history.IsSuccess || history.Data!.TotalCount != 2 || history.Data.Items.Count(x => x.Status == 3) != 1
                || history.Data.Items.Count(x => x.Status == 5) != 1)
                throw new InvalidOperationException("Değişmez deneme geçmişi kontrolü başarısız.");
            await Change(2, true);
            if ((await Notice(2)).Status != CollectionSmsStatus.Failed
                || !await db.Set<CollectionContractRatePeriod>().AnyAsync(x => x.ContractId == contracts[2].Id && x.Amount == 1250))
                throw new InvalidOperationException("SMS hazırlama hatası finansal zam kaydını bozdu.");
            await Change(3, true); var interrupted = await Notice(3);
            await db.Set<CollectionSmsNotification>().Where(x => x.Id == interrupted.Id).ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, CollectionSmsStatus.Sending).SetProperty(x => x.UpdatedDate, DateTimeOffset.UtcNow.AddMinutes(-3)));
            await db.Set<CollectionSmsAttempt>().Where(x => x.NotificationId == interrupted.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, CollectionSmsStatus.Sending));
            await Process(interrupted.Id);
            if ((await Notice(3)).Status != CollectionSmsStatus.Unknown || (await Preview(3)).CanSend || sender.Calls != 1)
                throw new InvalidOperationException("Kesilen gönderimin belirsiz sonuç koruması başarısız.");
            if (contracts.Length == 5)
            {
                await Change(4, true); var group = await Notice(4);
                var recipient = await db.Set<CollectionSmsAttempt>().Where(x => x.NotificationId == group.Id).Select(x => x.RecipientCustomerId).SingleAsync();
                if (recipient != parent!.CustomerId) throw new InvalidOperationException("Grup sorumlusuna eşleşme kontrolü başarısız.");
            }
            var trackingQuery = new CollectionSmsTrackingQuery { StartDate = today, EndDate = today, Page = 1, PageSize = 25, LatestOnly = true };
            var tracking = await sms.TrackingAsync(trackingQuery, default);
            if (!tracking.IsSuccess || tracking.Data!.Items.Count(x => x.ContractId == contracts[1].Id) != 1
                || tracking.Data.Items.Single(x => x.ContractId == contracts[1].Id).Attempt.Status != 5)
                throw new InvalidOperationException("SMS son deneme SQL takip sorgusu başarısız.");
            trackingQuery.LatestOnly = false; trackingQuery.Status = 3;
            tracking = await sms.TrackingAsync(trackingQuery, default);
            if (!tracking.IsSuccess || tracking.Data!.Items.Any(x => x.Attempt.Status != 3)
                || !tracking.Data.Items.Any(x => x.ContractId == contracts[1].Id))
                throw new InvalidOperationException("SMS tüm denemeler/durum SQL filtresi başarısız.");
            await db.Set<CollectionContractRatePeriod>().Where(x => x.ContractId == contracts[0].Id).ExecuteDeleteAsync();
            await db.Set<CollectionContract>().Where(x => x.Id == contracts[0].Id).ExecuteDeleteAsync();
            trackingQuery.LatestOnly = true; trackingQuery.Status = null;
            tracking = await sms.TrackingAsync(trackingQuery, default);
            if (!tracking.IsSuccess || !tracking.Data!.Items.Any(x => x.ContractId == contracts[0].Id && !x.ContractExists))
                throw new InvalidOperationException("Silinmiş finansal kayıtların SMS kanıtı takip edilemiyor.");
            Console.WriteLine("Genel SMS takip SQL kontrolü başarılı: son/tüm denemeler, durum/tarih filtreleri ve kaldırılmış sözleşmede SMS kanıtı korunuyor.");
            Console.WriteLine($"SMS iş akışı başarılı: atomik zam/kuyruk, eşzamanlı claim, simülasyon, manuel önizleme ve mükerrer reddi, hata/yeniden deneme geçmişi, sıfır tutar koruması, kesilen gönderim koruması; grup üst kart kontrolü: {contracts.Length == 5}. Gerçek SMS gönderilmedi.");
        }
        finally
        {
            db.ChangeTracker.Clear(); var ids = contracts.Where(x => x.Id > 0).Select(x => x.Id).ToArray();
            await db.Set<CollectionSmsAttempt>().Where(x => ids.Contains(x.Notification.ContractId)).ExecuteDeleteAsync();
            await db.Set<CollectionSmsNotification>().Where(x => ids.Contains(x.ContractId)).ExecuteDeleteAsync();
            await db.Set<CollectionContractRatePeriod>().Where(x => ids.Contains(x.ContractId)).ExecuteDeleteAsync();
            await db.Set<CollectionContract>().Where(x => ids.Contains(x.Id) && x.GtsNo != null && x.GtsNo.StartsWith(marker)).ExecuteDeleteAsync();
            Console.WriteLine("Yalnız bu kontrolün oluşturduğu geçici collection kayıtları temizlendi; mevcut müşteri, sözleşme ve ödeme verileri değiştirilmedi.");
        }
    }

    private sealed class RejectSender : ISmsSender
    {
        public int Calls { get; private set; }
        public Task<SmsSendResult> SendAsync(string phone, string message, string correlationId, CancellationToken ct = default)
        {
            Calls++; return Task.FromResult(new SmsSendResult(SmsSendStatus.Rejected, "Kontrollü servis reddi.", ErrorCode: "CHECK_REJECTION"));
        }
    }
    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "SMS kontrolü";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
