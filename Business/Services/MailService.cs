using Business.Interfaces;
using Business.UnitOfWork;
using Core.Common;
using Core.Enums;
using Core.Utilities.Constants;
using Model.Concrete;
using System.Net;
using System.Net.Mail;
using System.Globalization;

namespace Business.Services
{
    public class MailService : IMailService
    {
        private readonly IUnitOfWork _uow;
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
        }

        public MailService(IUnitOfWork uow)
        {
            _uow = uow ?? throw new ArgumentNullException(nameof(uow));
            _cfg = LoadConfig(); // ⬅️ uygulama ayağa kalkarken tek sefer yükle
        }

        /// <summary>
        /// İstersen dışarıdan manuel çağırıp yeniden yükleyebilirsin (örn. panelden ayar değişti).
        /// </summary>
        public void ReloadConfig()
        {
            _cfg = LoadConfig();
        }

        private MailConfig LoadConfig()
         {
            try
            {
                // ctor’da await edemeyeceğimiz için sync bekleme
                var configuration = _uow.Repository
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
                    FromName = Get(CommonConstants.MailFromName, Messages.MGSHelpDesk)
                };
            }
            catch (Exception ex)
            {
                // Fail fast: yanlış mail konfigürasyonu varsa servis başında patlasın
                throw new InvalidOperationException("Mail konfigürasyonu yüklenemedi.", ex);
            }
        }

        public Task<ResponseModel<bool>> SendLocationOverrideMailAsync(List<string> managers, string subject, string html)
        {
            try
            {
                if (managers is null || managers.Count == 0)
                    return Task.FromResult(new ResponseModel<bool>(false, false, "Alıcı bulunamadı.", StatusCode.BadRequest));

                var tos = string.Join(";", managers);

                SendMail(
                    from: _cfg.From,
                    tos: tos,
                    ccs: "",
                    subject: subject,
                    body: html,
                    isHtml: true,
                    mailServer: _cfg.Server,
                    mailServerPort: _cfg.Port,
                    useSsl: _cfg.UseSsl,
                    useCredential: true,
                    user: _cfg.User,
                    pass: _cfg.Pass,
                    domain: "",
                    fromName: _cfg.FromName
                );

                return Task.FromResult(new ResponseModel<bool>(true, true, Messages.MailSentSuccessfully, StatusCode.Ok));
            }
            catch (Exception ex)
            {
                return Task.FromResult(new ResponseModel<bool>(false, false, $"{Messages.MailSendFailed} {ex.Message}", StatusCode.Ok));
            }
        }

        public Task<ResponseModel<bool>> SendResetPassMailAsync(string bodyMesage, string to)
        {
            try
            {
                string subject = "MGS - Şifre Değiştirme";

                SendMail(
                    from: _cfg.From,
                    tos: to,
                    ccs: "",
                    subject: subject,
                    body: bodyMesage,
                    isHtml: true,
                    mailServer: _cfg.Server,
                    mailServerPort: _cfg.Port,
                    useSsl: _cfg.UseSsl,
                    useCredential: true,
                    user: _cfg.User,
                    pass: _cfg.Pass,
                    domain: "",
                    fromName: _cfg.FromName
                );

                return Task.FromResult(new ResponseModel<bool>(true, true, Messages.MailSentSuccessfully, StatusCode.Ok));
            }
            catch (Exception ex)
            {
                return Task.FromResult(new ResponseModel<bool>(false, false, $"{Messages.MailSendFailed} {ex.Message}", StatusCode.Ok));
            }
        }

        public static void SendMail(
            string from, string tos, string ccs, string subject,
            string body, bool isHtml, string mailServer, int mailServerPort, bool useSsl,
            bool useCredential, string user, string pass, string domain, string fromName, string attachment = "")
        {
            try
            {
                var tosArray = (tos ?? string.Empty).Replace(",", ";").Split(';', StringSplitOptions.RemoveEmptyEntries);
                var ccsArray = (ccs ?? string.Empty).Replace(",", ";").Split(';', StringSplitOptions.RemoveEmptyEntries);

                using var mail = new MailMessage
                {
                    From = new MailAddress(from, fromName),
                    Subject = subject,
                    IsBodyHtml = isHtml,
                    Body = body
                };

                foreach (var a in tosArray) { try { mail.To.Add(new MailAddress(a.Trim())); } catch { } }
                foreach (var a in ccsArray) { try { mail.CC.Add(new MailAddress(a.Trim())); } catch { } }

                if (!string.IsNullOrWhiteSpace(attachment))
                    mail.Attachments.Add(new Attachment(attachment));

                using var smtp = new SmtpClient(mailServer, mailServerPort)
                {
                    EnableSsl = useSsl
                };

                if (useCredential)
                {
                    smtp.Credentials = string.IsNullOrWhiteSpace(domain)
                        ? new NetworkCredential(user, pass)
                        : new NetworkCredential(user, pass, domain);
                }

                // TEST ortamı: sertifika doğrulamasını atla (PROD’da kaldırın)
                //ServicePointManager.ServerCertificateValidationCallback = (s, cert, chain, sslPolicyErrors) => true;

                smtp.Send(mail);
            }
            catch
            {
                throw;
            }
        }
    }
}
