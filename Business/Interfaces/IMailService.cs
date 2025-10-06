using Core.Common;

namespace Business.Interfaces
{
    public interface IMailService
    {
        Task<ResponseModel<bool>> SendResetPassMailAsync(string bodyMesage, string to);
    }
}
