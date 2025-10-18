using Model.Dtos.CustomerGroupProductPrice;

namespace Business.Interfaces
{
    public interface ICustomerGroupProductPriceService
    {
        /// <summary>
        /// Ürün ve müşteri grubu bazlı fiyat kayıtlarını getirir.
        /// </summary>
        Task<List<CustomerGroupProductPriceGetDto>> GetByProductAndCustomerGroupAsync(long productId, long customerGroupId);
    }
}
