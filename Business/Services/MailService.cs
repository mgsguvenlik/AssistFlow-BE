using Business.Interfaces;
using Business.UnitOfWork;
using Core.Common;
using Core.Enums;
using Core.Utilities.Constants;
using Model.Concrete;
using System.Net;
using System.Net.Mail;

namespace Business.Services
{
    public class MailService : IMailService
    {
        private readonly IUnitOfWork _uow;
        public MailService(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<ResponseModel<bool>> SendResetPassMailAsync(string bodyMesage, string to)
        {
            ///MZK Not: Burası düzenlenecek
            try
            {
                var configuration = await _uow.Repository.GetMultipleAsync<Configuration>(true);
                string mailserver = configuration.FirstOrDefault(x => x.Name == "MailServer")?.Value ?? "";
                string mailserverportValue = configuration.FirstOrDefault(x => x.Name == "MailServerPort")?.Value ?? "";
                int mailserverport = !string.IsNullOrEmpty(mailserverportValue) ? int.Parse(mailserverportValue) : 0;
                bool useSSL = configuration.FirstOrDefault(x => x.Name == "MailUseSSL")?.Value?.ToLower() == "1";
                string user = configuration.FirstOrDefault(x => x.Name == "MailUser")?.Value ?? "";
                string pass = configuration.FirstOrDefault(x => x.Name == "MailPassword")?.Value ?? "";
                string from = configuration.FirstOrDefault(x => x.Name == "MailFrom")?.Value ?? "";
                string subject = Messages.CityNotFound;

                SendMail(from, to, "", subject, bodyMesage, true, mailserver, mailserverport, useSSL, true, user, pass, "", "MGS Destek");
                return new ResponseModel<bool>(true, true, Messages.MailSentSuccessfully, StatusCode.Ok);
            }
            catch (Exception ex)
            {
                return new ResponseModel<bool>(false, false, $"{Messages.MailSendFailed} {ex.Message}", StatusCode.Ok);
            }
        }

        public static void SendMail(string from, string tos, string ccs, string subject,
            string body, bool isHtml, string mailServer, int mailServerPort, bool useSsl,
            bool useCredential, string user, string pass, string domain, string fromName, string attachment = "")
        {
            try
            {
                string[] tosArray = tos.ToString().Replace(",", ";").Split(';');
                string[] ccsArray = ccs.ToString().Replace(",", ";").Split(';');
                MailMessage mail = new MailMessage();
                mail.From = new MailAddress(from, fromName);
                try { foreach (string address in tosArray) { try { mail.To.Add(new MailAddress(address)); } catch { } } }
                catch { }
                if (ccs != null) try { foreach (string address in ccsArray) { try { mail.CC.Add(new MailAddress(address)); } catch { } } }
                    catch { }
                mail.Subject = subject;
                mail.IsBodyHtml = isHtml;
                mail.Body = body;
                SmtpClient smtp = new SmtpClient(mailServer, mailServerPort);
                smtp.EnableSsl = useSsl;
                if (useCredential)
                {
                    if (domain == "") smtp.Credentials = new System.Net.NetworkCredential(user, pass);
                    else smtp.Credentials = new System.Net.NetworkCredential(user, pass, domain);
                }
                if (attachment != "")
                {
                    Attachment mailAttachment;
                    mailAttachment = new Attachment(("" + attachment));
                    mail.Attachments.Add(mailAttachment);
                }

                // Geçici çözüm: Sertifika doğrulamasını atla (sadece test için!)
                ServicePointManager.ServerCertificateValidationCallback = (s, cert, chain, sslPolicyErrors) => true;
                smtp.Send(mail);
            }
            catch (Exception ex)
            {
                throw;
            }
        }


    }
}
