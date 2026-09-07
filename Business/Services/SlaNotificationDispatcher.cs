using Business.Interfaces;
using Business.UnitOfWork;
using Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Model.Concrete;
using Model.Concrete.Qnb;
using Model.Concrete.WorkFlows;
using Model.Concrete.Ykb;
using Model.Concrete.Ekb;

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
            var mailPush = scope.ServiceProvider.GetRequiredService<IMailPushService>();

            // 1) Aktif SLA ayarlarını getir
            var slaSettings = await uow.Repository
                .GetQueryable<WorkFlowSlaSetting>()
                .AsNoTracking()
                .Where(x => x.IsActive && !x.IsDeleted && !string.IsNullOrEmpty(x.NotificationEmails))
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
                    await ProcessSingleSlaSettingAsync(uow, mailPush, slaSetting, stoppingToken);
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
            IMailPushService mailPush,
            WorkFlowSlaSetting slaSetting,
            CancellationToken stoppingToken)
        {
            var now = DateTimeOffset.Now;

            // SLA süresi bitiş saati hesaplama
            // Örnek: SLA 240 saat, 48 saat önce bildirim → CreatedDate + 192 saat'ten büyükse bildirim gönder
            var notificationThresholdHours = slaSetting.SlaDurationHours - slaSetting.NotificationBeforeHours;

            // CustomerType'a göre WorkFlow'ları getir
            if (slaSetting.CustomerType == WorkFlowCustomerType.Individual)
            {
                await ProcessIndividualWorkFlowsAsync(uow, mailPush, slaSetting, notificationThresholdHours, now, stoppingToken);
            }
            else if (slaSetting.CustomerType == WorkFlowCustomerType.YKB)
            {
                await ProcessYkbWorkFlowsAsync(uow, mailPush, slaSetting, notificationThresholdHours, now, stoppingToken);
            }
            else if (slaSetting.CustomerType == WorkFlowCustomerType.EKB)
            {
                await ProcessEkbWorkFlowsAsync(uow, mailPush, slaSetting, notificationThresholdHours, now, stoppingToken);
            }
            else if (slaSetting.CustomerType == WorkFlowCustomerType.QNB)
            {
                await ProcessQnbWorkFlowsAsync(
                    uow,
                    mailPush,
                    slaSetting,
                    notificationThresholdHours,
                    now,
                    stoppingToken);
            }
            else
            {
                _logger.LogWarning("Desteklenmeyen CustomerType: {CustomerType}", slaSetting.CustomerType);
            }
        }

        #region Bireysel WorkFlow İşlemleri

        private async Task ProcessIndividualWorkFlowsAsync(
            IUnitOfWork uow,
            IMailPushService mailPush,
            WorkFlowSlaSetting slaSetting,
            int notificationThresholdHours,
            DateTimeOffset now,
            CancellationToken stoppingToken)
        {
            // WorkFlow tablosundan ilgili kayıtları getir
            var thresholdDate = now.AddHours(-notificationThresholdHours);

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
                    mailPush,
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
            IMailPushService mailPush,
            WorkFlowSlaSetting slaSetting,
            int notificationThresholdHours,
            DateTimeOffset now,
            CancellationToken stoppingToken)
        {
            // YkbWorkFlow tablosundan ilgili kayıtları getir
            var thresholdDate = now.AddHours(-notificationThresholdHours);

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
                    mailPush,
                    ykbWorkFlow.RequestNo,
                    slaSetting,
                    ykbWorkFlow.CreatedDate,
                    now,
                    stoppingToken);
            }
        }

        #endregion
        #region EKB WorkFlow İşlemleri

        private async Task ProcessEkbWorkFlowsAsync(
            IUnitOfWork uow,
            IMailPushService mailPush,
            WorkFlowSlaSetting slaSetting,
            int notificationThresholdHours,
            DateTimeOffset now,
            CancellationToken stoppingToken)
        {
            // EkbWorkFlow tablosundan ilgili kayıtları getir
            var thresholdDate = now.AddHours(-notificationThresholdHours);

            var ekbWorkFlows = await uow.Repository
                .GetQueryable<EkbWorkFlow>()
                .AsNoTracking()
                .Where(x => !x.IsDeleted
                    && x.WorkFlowStatus == WorkFlowStatus.Pending
                    && x.Priority == slaSetting.Priority
                    && x.CreatedDate <= thresholdDate)
                .ToListAsync(stoppingToken);

            _logger.LogDebug(
                "EKB - Priority: {Priority}, Threshold: {Threshold}, WorkFlow Sayısı: {Count}",
                slaSetting.Priority, thresholdDate, ekbWorkFlows.Count);

            foreach (var ekbWorkFlow in ekbWorkFlows)
            {
                await CreateSlaNotificationMailAsync(
                    uow,
                    mailPush,
                    ekbWorkFlow.RequestNo,
                    slaSetting,
                    ekbWorkFlow.CreatedDate,
                    now,
                    stoppingToken);
            }
        }

        #endregion

        #region QNB WorkFlow İşlemleri

        private async Task ProcessQnbWorkFlowsAsync(
            IUnitOfWork uow,
            IMailPushService mailPush,
            WorkFlowSlaSetting slaSetting,
            int notificationThresholdHours,
            DateTimeOffset now,
            CancellationToken stoppingToken)
        {
            var thresholdDate = now.AddHours(-notificationThresholdHours);

            var qnbWorkFlows = await uow.Repository
                .GetQueryable<QnbWorkFlow>()
                .AsNoTracking()
                .Where(x => !x.IsDeleted
                    && x.WorkFlowStatus == WorkFlowStatus.Pending
                    && x.Priority == slaSetting.Priority
                    && x.CreatedDate <= thresholdDate)
                .ToListAsync(stoppingToken);

            _logger.LogDebug(
                "QNB - Priority: {Priority}, Threshold: {Threshold}, WorkFlow Sayısı: {Count}",
                slaSetting.Priority,
                thresholdDate,
                qnbWorkFlows.Count);

            foreach (var qnbWorkFlow in qnbWorkFlows)
            {
                await CreateSlaNotificationMailAsync(
                    uow,
                    mailPush,
                    qnbWorkFlow.RequestNo,
                    slaSetting,
                    qnbWorkFlow.CreatedDate,
                    now,
                    stoppingToken);
            }
        }

        #endregion

        #region Mail Oluşturma
        private async Task CreateSlaNotificationMailAsync(
            IUnitOfWork uow,
            IMailPushService mailPush,
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

            // SLA bitiş saati
            var slaDeadline = createdDate.AddHours(slaSetting.SlaDurationHours);
            var remainingHours = (slaDeadline - now).TotalHours;

            // Mail içeriği oluştur
            var body = GenerateSlaNotificationBody(
                requestNo,
                slaSetting.Priority.ToString(),
                createdDate,
                slaDeadline,
                remainingHours);

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
                "SLA bildirimi oluşturuldu. RequestNo: {RequestNo}, Kalan Saat: {RemainingHours:F1}",
                requestNo, remainingHours);
        }

        private static string FormatRemainingTime(double totalHours)
        {
            var isNegative = totalHours < 0;
            var absHours = Math.Abs(totalHours);

            var days = (int)(absHours / 24);
            var hours = absHours % 24;

            string result;
            if (days > 0)
            {
                // Saat kısmı neredeyse 0 ise sadece gün göster (örn. 48 saat -> "2 gün")
                result = hours >= 0.1
                    ? $"{days} gün {hours:F1} saat"
                    : $"{days} gün";
            }
            else
            {
                result = $"{hours:F1} saat";
            }

            return isNegative ? $"-{result}" : result;
        }

        private static string GenerateSlaNotificationBody(
            string requestNo,
            string priority,
            DateTimeOffset createdDate,
            DateTimeOffset slaDeadline,
            double remainingHours)
        {
            var remainingText = FormatRemainingTime(remainingHours);

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
                                        <span class='warning'>{remainingText}</span>
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