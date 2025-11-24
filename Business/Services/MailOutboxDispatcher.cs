using Business.Interfaces;
using Business.UnitOfWork;
using Core.Enums;
using Core.Utilities.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Model.Concrete;
using Model.Concrete.WorkFlows;
using System.Globalization;

namespace Business.Services
{
    public class MailOutboxDispatcher : BackgroundService
    {
        private readonly IServiceProvider _sp;
        private readonly ILogger<MailOutboxDispatcher> _log;
        private MailConfig _cfg;
        private sealed class MailConfig
        {
            public string Server { get; init; } = "";
            public int Port { get; init; } = 25;
            public bool UseSsl { get; init; }
            public string User { get; init; } = "";
            public string Pass { get; init; } = "";
            public string From { get; init; } = "";
            public string FromName { get; init; } = "";
            public string Domain { get; init; } = "";
        }
        // basit ayarlar
        private readonly int _batchSize = 20;
        private readonly TimeSpan _poll = TimeSpan.FromSeconds(15);

        public MailOutboxDispatcher(IServiceProvider sp, ILogger<MailOutboxDispatcher> log)
        {
            _sp = sp; _log = log;
            _cfg = LoadConfig();
        }
        private MailConfig LoadConfig()
        {
            try
            {
                using var scope = _sp.CreateScope();
                var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                // ctor’da await edemeyeceğimiz için sync bekleme
                var configuration = uow.Repository
                    .GetMultipleAsync<Configuration>(true)
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();

                string Get(string name, string @default = "") =>
                    configuration.FirstOrDefault(x => x.Name == name)?.Value ?? @default;

                var portStr = Get(CommonConstants.MailServerPort, "25");
                var useSslStr = Get(CommonConstants.MailUseSSL, "0");
                return new MailConfig
                {
                    Server = Get(CommonConstants.MailServer),
                    Port = int.TryParse(portStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var p) ? p : 25,
                    UseSsl = useSslStr.Trim() is "1" or "true" or "True",
                    User = Get(CommonConstants.MailUser),
                    Pass = Get(CommonConstants.MailPassword),
                    From = Get(CommonConstants.MailFrom),
                    FromName = Get(CommonConstants.MailFromName, Messages.MGSHelpDesk),
                    Domain = Get(CommonConstants.MailDomain, Messages.MailDomain)
                };
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Mail konfigürasyonu yüklenemedi.", ex);
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _sp.CreateScope();
                    var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                    var mailService = scope.ServiceProvider.GetRequiredService<IMailService>();

                    var now = DateTime.Now;

                    var items = await uow.Repository
                        .GetQueryable<MailOutbox>(x =>
                            x.Status == MailOutboxStatus.Pending &&
                            (x.NextAttemptAt == null || x.NextAttemptAt <= now))
                        .OrderBy(x => x.CreatedDate)
                        .Take(_batchSize)
                        .ToListAsync(stoppingToken);

                    if (items.Count == 0)
                    {
                        await Task.Delay(_poll, stoppingToken);
                        continue;
                    }

                    foreach (var m in items)
                    {
                        try
                        {
                            m.Status = MailOutboxStatus.InProgress;
                            m.UpdatedDate = DateTime.Now;
                            uow.Repository.Update(m);

                            await uow.Repository.CompleteAsync();

                            // GÖNDER
                            MailService.SendMail(
                                from: _cfg.From, // MailService iç config From/FromName kullansın isterseniz oradan çekebilirsiniz
                                tos: m.ToRecipients,
                                ccs: m.CcRecipients ?? "",
                                subject: m.Subject,
                                body: m.BodyHtml,
                                isHtml: true,
                                mailServer: _cfg.Server,              // MailService iç LoadConfig ile dolduruyor; isterseniz wrapper kullanın
                                mailServerPort: 0,
                                useSsl: _cfg.UseSsl,
                                useCredential: false,
                                user: _cfg.User,
                                pass: _cfg.Pass,
                                domain: _cfg.Domain,
                                fromName: _cfg.FromName
                            );

                            m.Status = MailOutboxStatus.Sent;
                            m.UpdatedDate = DateTime.Now;
                            uow.Repository.Update(m);


                            var request = await uow.Repository
                                .GetQueryable<ServicesRequest>()
                                .Include(x => x.Customer)
                                .FirstOrDefaultAsync(x => x.RequestNo == m.RequestNo);

                            await uow.Repository.AddAsync(new WorkFlowActivityRecord
                            {
                                RequestNo = m.RequestNo,
                                FromStepCode = m.FromStepCode,
                                ToStepCode = m.ToStepCode,
                                ActionType = WorkFlowActionType.MailSent,
                                Summary = $"Mail gönderildi: {m.Subject}",
                                OccurredAtUtc = DateTime.Now,
                                CustomerId = request?.CustomerId ?? null
                            });

                            await uow.Repository.CompleteAsync();
                        }
                        catch (Exception exSend)
                        {
                            _log.LogError(exSend, "Mail gönderilemedi, id={Id}", m.Id);

                            m.TryCount += 1;
                            m.LastError = exSend.Message;
                            m.Status = (m.TryCount >= m.MaxTry) ? MailOutboxStatus.Failed : MailOutboxStatus.Pending;
                            // basit exponential backoff
                            m.NextAttemptAt = DateTime.Now.AddMinutes(Math.Min(60, (int)Math.Pow(2, m.TryCount)));

                            uow.Repository.Update(m);

                            await uow.Repository.AddAsync(new WorkFlowActivityRecord
                            {
                                RequestNo = m.RequestNo,
                                FromStepCode = m.FromStepCode,
                                ToStepCode = m.ToStepCode,
                                ActionType = WorkFlowActionType.MailSendFailed,
                                Summary = $"Mail gönderimi başarısız: {exSend.Message}",
                                OccurredAtUtc = DateTime.Now
                            });

                            await uow.Repository.CompleteAsync();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "MailOutboxDispatcher döngü hatası");
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
            }
        }
    }
}
