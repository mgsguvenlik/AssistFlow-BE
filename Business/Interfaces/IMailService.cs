using Core.Common;

namespace Business.Interfaces
{
    public interface IMailService
    {
        Task<ResponseModel<bool>> SendResetPassMailAsync(string bodyMesage, string to);
        Task<ResponseModel<bool>> SendLocationOverrideMailAsync(List<string> managers, string subject, string html);
    }
}
