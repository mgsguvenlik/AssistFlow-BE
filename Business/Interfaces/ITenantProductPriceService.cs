using Core.Common;
using Model.Dtos.TenantProductPrice;

namespace Business.Interfaces
{
    public interface ITenantProductPriceService
    {
        /// <summary>
        /// Ürün ve tenant bazlý fiyat kayýtlarýný getirir.
        /// </summary>
        Task<List<TenantProductPriceGetDto>> GetByProductAndTenantAsync(long productId, long tenantId);

        /// <summary>
        /// Filtreli sayfalama desteði.
        /// </summary>
        Task<ResponseModel<PagedResult<TenantProductPriceGetDto>>> GetPagedWithFilterAsync(TenantProductPriceQueryParams q);
    }
}