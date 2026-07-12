using Business.Interfaces;
using Core.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.ProductPriceAdjustmentRule;

namespace WebAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class ProductPriceAdjustmentRulesController
        : CrudControllerBase<
            ProductPriceAdjustmentRuleCreateDto,
            ProductPriceAdjustmentRuleUpdateDto,
            ProductPriceAdjustmentRuleGetDto,
            long>
    {
        private readonly IProductPriceAdjustmentRuleService _ruleService;

        public ProductPriceAdjustmentRulesController(
            ICrudService<
                ProductPriceAdjustmentRuleCreateDto,
                ProductPriceAdjustmentRuleUpdateDto,
                ProductPriceAdjustmentRuleGetDto,
                long> service,
            ILogger<ProductPriceAdjustmentRulesController> logger,
            IProductPriceAdjustmentRuleService ruleService)
            : base(service, logger)
        {
            _ruleService = ruleService;
        }

        /// <summary>
        /// Tenant'a ait bütün fiyat düzenleme kurallarını getirir.
        /// </summary>
        [HttpGet("get-by-tenant/{tenantId:long}")]
        public async Task<IActionResult> GetByTenant(long tenantId)
        {
            var response = await _ruleService.GetByTenantIdAsync(tenantId);
            return ToActionResult(response);
        }

        /// <summary>
        /// Tenant ve ürün için tanımlanmış aktif kuralları getirir.
        /// </summary>
        [HttpGet("get-active-by-product")]
        public async Task<IActionResult> GetActiveByProduct(
            [FromQuery] long tenantId,
            [FromQuery] long productId)
        {
            if (tenantId <= 0 || productId <= 0)
            {
                return BadRequest(
                    ResponseModel.Fail(
                        "tenantId ve productId zorunludur.",
                        Core.Enums.StatusCode.BadRequest));
            }

            var response =
                await _ruleService.GetActiveByTenantAndProductAsync(
                    tenantId,
                    productId);

            return ToActionResult(response);
        }
    }
}