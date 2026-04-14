using Core.Common;
using Model.Dtos.Customer;

namespace Business.Interfaces
{
    public interface ICustomerService
    {
        Task<ResponseModel<int>> ImportFromFileAsync(string filePath);
        Task<ResponseModel<PaginatedList<CustomerGetDto>>> GetByTenantCodeAsync(string? tenantCode, QueryParams queryParams);
    }
}
