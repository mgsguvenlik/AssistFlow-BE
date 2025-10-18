using Business.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.CustomerGroupProductPrice;

namespace WebAPI.Controllers
{


    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class CustomerGroupProductPricesController : CrudControllerBase<CustomerGroupProductPriceCreateDto, CustomerGroupProductPriceUpdateDto, CustomerGroupProductPriceGetDto, long>
    {

        private readonly ICustomerGroupProductPriceService _customerGroupProductPriceService;
        public CustomerGroupProductPricesController(
            ICrudService<CustomerGroupProductPriceCreateDto, CustomerGroupProductPriceUpdateDto, CustomerGroupProductPriceGetDto, long> service,
            ILogger<CustomerGroupProductPricesController> logger,
            ICustomerGroupProductPriceService customerGroupProductPriceService) : base(service, logger)
        {
            _customerGroupProductPriceService = customerGroupProductPriceService;
        }

        /// <summary>
        /// Belirtilen ürün ve müşteri grubu için fiyat kayıtlarını getirir.
        /// </summary>
        /// <param name="productId">Ürün ID</param>
        /// <param name="customerGroupId">Müşteri Grubu ID</param>
        /// <returns>İlgili fiyat kayıtlarının listesi</returns>
        [HttpGet("get-by-product-and-group")]
        public async Task<IActionResult> GetByProductAndCustomerGroup([FromQuery] long productId, [FromQuery] long customerGroupId)
        {
            var result = await _customerGroupProductPriceService.GetByProductAndCustomerGroupAsync(productId, customerGroupId);

            if (result == null || !result.Any())
                return NotFound(new { message = "Kayıt bulunamadı." });

            return Ok(result);
        }
    }
}
