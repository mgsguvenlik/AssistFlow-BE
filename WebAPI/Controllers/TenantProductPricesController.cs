using Business.Interfaces;
using Core.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.TenantProductPrice;

namespace WebAPI.Controllers
{
    [Authorize] 
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class TenantProductPricesController : CrudControllerBase<TenantProductPriceCreateDto, TenantProductPriceUpdateDto, TenantProductPriceGetDto, long>
    {
        private readonly ITenantProductPriceService _tenantProductPriceService;

        public TenantProductPricesController(
            ICrudService<TenantProductPriceCreateDto, TenantProductPriceUpdateDto, TenantProductPriceGetDto, long> service,
            ILogger<TenantProductPricesController> logger,
            ITenantProductPriceService tenantProductPriceService) : base(service, logger)
        {
            _tenantProductPriceService = tenantProductPriceService;
        }

        /// <summary>
        /// Filtreli sayfalama ile tenant ürün fiyatlarýný getirir.
        /// </summary>
        /// <param name="q">Sorgu parametreleri (ProductId, TenantId filtreleri dahil)</param>
        /// <returns>Sayfalanmýþ fiyat listesi</returns>
        [HttpGet]
        public override async Task<IActionResult> GetPaged([FromQuery] QueryParams q)
        {
            // Eðer Filter.ProductId veya Filter.TenantId varsa özel metodu kullan
            if (HttpContext.Request.Query.ContainsKey("Filter.ProductId") || 
                HttpContext.Request.Query.ContainsKey("Filter.TenantId"))
            {
                var filterQuery = new TenantProductPriceQueryParams
                {
                    Page = q.Page,
                    PageSize = q.PageSize,
                    Search = q.Search,
                    Sort = q.Sort,
                    Desc = q.Desc,
                    ProductId = HttpContext.Request.Query.TryGetValue("Filter.ProductId", out var pId) && long.TryParse(pId, out var pid) ? pid : null,
                    TenantId = HttpContext.Request.Query.TryGetValue("Filter.TenantId", out var tId) && long.TryParse(tId, out var tid) ? tid : null
                };

                var result = await _tenantProductPriceService.GetPagedWithFilterAsync(filterQuery);
                return ToActionResult(result);
            }

            // Yoksa base metodunu kullan
            return await base.GetPaged(q);
        }

        /// <summary>
        /// Belirtilen ürün ve tenant için fiyat kayýtlarýný getirir.
        /// </summary>
        /// <param name="productId">Ürün ID</param>
        /// <param name="tenantId">Tenant ID</param>
        /// <returns>Ýlgili fiyat kayýtlarýnýn listesi</returns>
        [HttpGet("get-by-product-and-tenant")]
        public async Task<IActionResult> GetByProductAndTenant([FromQuery] long productId, [FromQuery] long tenantId)
        {
            var result = await _tenantProductPriceService.GetByProductAndTenantAsync(productId, tenantId);

            if (result == null || !result.Any())
                return NotFound(new { message = "Kayýt bulunamadý." });

            return Ok(result);
        }
    }
}