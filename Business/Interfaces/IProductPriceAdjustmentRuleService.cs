using Core.Common;
using Model.Dtos.ProductPriceAdjustmentRule;

namespace Business.Interfaces
{
    public interface IProductPriceAdjustmentRuleService
    {
        Task<ResponseModel<List<ProductPriceAdjustmentRuleGetDto>>>
            GetByTenantIdAsync(long tenantId);

        Task<ResponseModel<List<ProductPriceAdjustmentRuleGetDto>>>
            GetActiveByTenantAndProductAsync(long tenantId, long productId);
    }
}