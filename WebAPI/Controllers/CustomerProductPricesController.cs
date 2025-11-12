using Business.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.CustomerProductPrice;

namespace WebAPI.Controllers
{

    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class CustomerProductPricesController : CrudControllerBase<CustomerProductPriceCreateDto, CustomerProductPriceUpdateDto, CustomerProductPriceGetDto, long>
    {
        private readonly ICustomerProductPriceService _customerProductPriceService;
        public CustomerProductPricesController(
            ICrudService<CustomerProductPriceCreateDto, CustomerProductPriceUpdateDto, CustomerProductPriceGetDto, long> service,
            ILogger<CustomerProductPricesController> logger,
            ICustomerProductPriceService customerProductPriceService) : base(service, logger)
        {
            _customerProductPriceService = customerProductPriceService;
        }

        /// <summary>
        /// Belirtilen ürün ve müşteri için fiyat kayıtlarını getirir.
        /// </summary>
        /// <param name="productId">Ürün ID</param>
        /// <param name="customerId">Müşteri ID</param>
        /// <returns>İlgili fiyat kayıtlarının listesi</returns>
        [HttpGet("get-by-product-and-customer")]
        public async Task<IActionResult> GetByProductAndCustomerGroup([FromQuery] long productId, [FromQuery] long customerId)
        {
            var result = await _customerProductPriceService.GetByProductAndCustomerAsync(productId, customerId);
            if (result == null || !result.Any())
                return NotFound(new { message = "Kayıt bulunamadı." });

            return Ok(result);
        }
    }
}
