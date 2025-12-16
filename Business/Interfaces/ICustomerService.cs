using Core.Common;

namespace Business.Interfaces
{
    public interface ICustomerService
    {
        Task<ResponseModel<int>> ImportFromFileAsync(string filePath);
    }
}
