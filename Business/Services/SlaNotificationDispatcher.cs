using Business.Interfaces;
using Business.UnitOfWork;
using Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Model.Concrete;
using Model.Concrete.WorkFlows;
using Model.Concrete.Ykb;

namespace Business.Services
{
    /// <summary>
    /// SLA süresi dolmak üzere olan WorkFlow'lar için mail bildirimi oluşturan background service
    /// </summary>
    public class SlaNotificationDispatcher : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SlaNotificationDispatcher> _logger;

        // Her 1 dakikada bir kontrol
        private readonly TimeSpan _pollInterval = TimeSpan.FromMinutes(1);

        public SlaNotificationDispatcher(
            IServiceProvider serviceProvider,
            ILogger<SlaNotificationDispatcher> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SlaNotificationDispatcher başlatıldı.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessSlaNotificationsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SlaNotificationDispatcher döngü hatası");
                }

                // 1 dakika bekle
                await Task.Delay(_pollInterval, stoppingToken);
            }

            _logger.LogInformation("SlaNotificationDispatcher durduruldu.");
        }

        private async Task ProcessSlaNotificationsAsync(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var mailPush = scope.ServiceProvider.GetRequiredService<IMailPushService>(); // 🔹 Eklendi

            // 1) Aktif SLA ayarlarını getir
            var slaSettings = await uow.Repository
                .GetQueryable<WorkFlowSlaSetting>()
                .AsNoTracking()
                .Where(x => x.IsActive && !x.IsDeleted)
                .ToListAsync(stoppingToken);

            if (!slaSettings.Any())
            {
                _logger.LogDebug("Aktif SLA ayarı bulunamadı.");
                return;
            }

            _logger.LogInformation("{Count} aktif SLA ayarı bulundu, kontrol ediliyor...", slaSettings.Count);

            foreach (var slaSetting in slaSettings)
            {
                try
                {
                    await ProcessSingleSlaSettingAsync(uow, mailPush, slaSetting, stoppingToken); // 🔹 mailPush eklendi
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, 
                        "SLA ayarı işlenirken hata. CustomerType: {CustomerType}, Priority: {Priority}",
                        slaSetting.CustomerType, slaSetting.Priority);
                }
            }
        }

        private async Task ProcessSingleSlaSettingAsync(
            IUnitOfWork uow,
            IMailPushService mailPush, // 🔹 Parametre eklendi
            WorkFlowSlaSetting slaSetting,
            CancellationToken stoppingToken)
        {
            var now = DateTimeOffset.Now;

            // SLA süresi bitiş tarihi hesaplama
            // Örnek: SLA 10 gün, 2 gün önce bildirim → CreatedDate + 8 gün'den büyükse bildirim gönder
            var notificationThresholdDays = slaSetting.SlaDurationDays - slaSetting.NotificationBeforeDays;

            // CustomerType'a göre WorkFlow'ları getir
            if (slaSetting.CustomerType == WorkFlowCustomerType.Individual)
            {
                await ProcessIndividualWorkFlowsAsync(uow, mailPush, slaSetting, notificationThresholdDays, now, stoppingToken);
            }
            else if (slaSetting.CustomerType == WorkFlowCustomerType.YKB)
            {
                await ProcessYkbWorkFlowsAsync(uow, mailPush, slaSetting, notificationThresholdDays, now, stoppingToken);
            }
            else
            {
                _logger.LogWarning("Desteklenmeyen CustomerType: {CustomerType}", slaSetting.CustomerType);
            }
        }

        #region Individual WorkFlow İşlemleri

        private async Task ProcessIndividualWorkFlowsAsync(
            IUnitOfWork uow,
            IMailPushService mailPush, // 🔹 Parametre eklendi
            WorkFlowSlaSetting slaSetting,
            int notificationThresholdDays,
            DateTimeOffset now,
            CancellationToken stoppingToken)
        {
            // WorkFlow tablosundan ilgili kayıtları getir
            var thresholdDate = now.AddDays(-notificationThresholdDays);

            var workFlows = await uow.Repository
                .GetQueryable<WorkFlow>()
                .AsNoTracking()
                .Where(x => !x.IsDeleted
                    && x.WorkFlowStatus == WorkFlowStatus.Pending
                    && x.Priority == slaSetting.Priority
                    && x.CreatedDate <= thresholdDate)
                .ToListAsync(stoppingToken);

            _logger.LogDebug(
                "Individual - Priority: {Priority}, Threshold: {Threshold}, WorkFlow Sayısı: {Count}",
                slaSetting.Priority, thresholdDate, workFlows.Count);

            foreach (var workFlow in workFlows)
            {
                await CreateSlaNotificationMailAsync(
                    uow,
                    mailPush, // 🔹 Parametre eklendi
                    workFlow.RequestNo,
                    slaSetting,
                    workFlow.CreatedDate,
                    now,
                    stoppingToken);
            }
        }

        #endregion

        #region YKB WorkFlow İşlemleri

        private async Task ProcessYkbWorkFlowsAsync(
            IUnitOfWork uow,
            IMailPushService mailPush, // 🔹 Parametre eklendi
            WorkFlowSlaSetting slaSetting,
            int notificationThresholdDays,
            DateTimeOffset now,
            CancellationToken stoppingToken)
        {
            // YkbWorkFlow tablosundan ilgili kayıtları getir
            var thresholdDate = now.AddDays(-notificationThresholdDays);

            var ykbWorkFlows = await uow.Repository
                .GetQueryable<YkbWorkFlow>()
                .AsNoTracking()
                .Where(x => !x.IsDeleted 
                    && x.WorkFlowStatus == WorkFlowStatus.Pending
                    && x.Priority == slaSetting.Priority
                    && x.CreatedDate <= thresholdDate)
                .ToListAsync(stoppingToken);

            _logger.LogDebug(
                "YKB - Priority: {Priority}, Threshold: {Threshold}, WorkFlow Sayısı: {Count}",
                slaSetting.Priority, thresholdDate, ykbWorkFlows.Count);

            foreach (var ykbWorkFlow in ykbWorkFlows)
            {
                await CreateSlaNotificationMailAsync(
                    uow,
                    mailPush, // 🔹 Parametre eklendi
                    ykbWorkFlow.RequestNo,
                    slaSetting,
                    ykbWorkFlow.CreatedDate,
                    now,
                    stoppingToken);
            }
        }

        #endregion

        #region Mail Oluşturma

        private async Task CreateSlaNotificationMailAsync(
            IUnitOfWork uow,
            IMailPushService mailPush, // 🔹 Parametre eklendi
            string requestNo,
            WorkFlowSlaSetting slaSetting,
            DateTimeOffset createdDate,
            DateTimeOffset now,
            CancellationToken stoppingToken)
        {
            // Subject belirleme
            var subject = $"SLA Uyarısı: {requestNo} - {slaSetting.Priority} Öncelik";

            // Aynı RequestNo ve Subject ile daha önce mail gönderilmiş mi kontrol et
            var existingMail = await uow.Repository
                .GetQueryable<MailOutbox>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => 
                    x.RequestNo == requestNo 
                    && x.Subject == subject, 
                    stoppingToken);

            if (existingMail != null)
            {
                _logger.LogDebug(
                    "RequestNo: {RequestNo} için SLA bildirimi daha önce gönderilmiş, tekrar gönderilmiyor.",
                    requestNo);
                return;
            }

            // SLA bitiş tarihi
            var slaDeadline = createdDate.AddDays(slaSetting.SlaDurationDays);
            var remainingDays = (slaDeadline - now).TotalDays;

            // Mail içeriği oluştur
            var body = GenerateSlaNotificationBody(
                requestNo,
                slaSetting.Priority.ToString(),
                createdDate,
                slaDeadline,
                remainingDays);

            // 🔹 mailPush.EnqueueAsync kullanımı (WorkFlowService ile aynı)
            await mailPush.EnqueueAsync(new MailOutbox
            {
                RequestNo = requestNo,
                FromStepCode = "SLA",
                ToStepCode = "NOTIFICATION",
                ToRecipients = slaSetting.NotificationEmails ?? string.Empty,
                Subject = subject,
                BodyHtml = body,
                Status = MailOutboxStatus.Pending,
                TryCount = 0,
                MaxTry = 5,
                CreatedDate = DateTime.Now,
                CreatedUser = null // Sistem tarafından oluşturuldu
            });

            _logger.LogInformation(
                "SLA bildirimi oluşturuldu. RequestNo: {RequestNo}, Kalan Gün: {RemainingDays:F1}",
                requestNo, remainingDays);
        }

        private static string GenerateSlaNotificationBody(
            string requestNo,
            string priority,
            DateTimeOffset createdDate,
            DateTimeOffset slaDeadline,
            double remainingDays)
        {
            return $@"
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; }}
        .container {{ max-width: 600px; margin: 20px auto; padding: 20px; border: 1px solid #ddd; }}
        .header {{ background-color: #f44336; color: white; padding: 10px; text-align: center; }}
        .content {{ padding: 20px; }}
        .info {{ margin: 10px 0; }}
        .label {{ font-weight: bold; }}
        .warning {{ color: #f44336; font-weight: bold; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h2>⚠️ SLA Uyarısı</h2>
        </div>
        <div class='content'>
            <p>Merhaba,</p>
            <p>Aşağıdaki iş akışının SLA süresi dolmak üzeredir:</p>

            <div class='info'>
                <span class='label'>İstek Numarası:</span> {requestNo}
            </div>
            <div class='info'>
                <span class='label'>Öncelik:</span> {priority}
            </div>
            <div class='info'>
                <span class='label'>Oluşturulma Tarihi:</span> {createdDate:dd.MM.yyyy HH:mm}
            </div>
            <div class='info'>
                <span class='label'>SLA Bitiş Tarihi:</span> {slaDeadline:dd.MM.yyyy HH:mm}
            </div>
            <div class='info'>
                <span class='label warning'>Kalan Süre:</span> 
                <span class='warning'>{remainingDays:F1} gün</span>
            </div>

            <p style='margin-top: 20px;'>
                Lütfen bu iş akışını en kısa sürede tamamlayınız.
            </p>

            <p>Saygılarımızla,<br/>MGS AssistFlow Sistem</p>
        </div>
    </div>
</body>
</html>";
        }

        #endregion
    }
}