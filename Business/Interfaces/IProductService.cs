using Core.Common;
using Model.Dtos.Product;
using System.Threading.Tasks;

namespace Business.Interfaces
{
    public interface IProductService
    {
        Task<ResponseModel<List<ProductEffectivePriceDto>>> GetProductsByCustomerIdAsync(long customerId);
        Task<ResponseModel<ProductEffectivePriceDto>> GetEffectivePriceAsync(long customerId, long productId);
        Task<ResponseModel<List<ProductEffectivePriceDto>>> GetEffectivePricesAsync(CustomerProductRequestDto dto);
    }
}
