using Core.Common;

using Business.Models;

namespace Business.Interfaces
{
    public interface IMailService
    {
        Task<ResponseModel<bool>> SendResetPassMailAsync(string bodyMesage, string to);
        Task<ResponseModel<bool>> SendLocationOverrideMailAsync(List<string> managers, string subject, string html);
        Task SendWithAttachmentAsync(
            IReadOnlyCollection<string> recipients,
            string subject,
            string htmlBody,
            MailAttachmentData attachment,
            CancellationToken cancellationToken = default);
    }
}
