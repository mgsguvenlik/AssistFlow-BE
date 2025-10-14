using Business.Interfaces;
using Core.Common;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.Product;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class ProductsController
        : CrudControllerBase<ProductCreateDto, ProductUpdateDto, ProductGetDto, long>
    {
        private readonly IProductService _productService;
        public ProductsController(
            ICrudService<ProductCreateDto, ProductUpdateDto, ProductGetDto, long> service,
            ILogger<ProductsController> logger,
            IProductService productService) : base(service, logger)
        {
            _productService = productService;
        }

        [HttpGet("get-by-customer/{customerId:long}")]
        public async Task<IActionResult> GetProductsByCustomer(long customerId)
        {
            var response = await _productService.GetProductsByCustomerIdAsync(customerId);
            return ToActionResult(response);
        }

        [HttpGet("get-effective-price")]
        public async Task<IActionResult> GetEffectivePrice([FromQuery] long customerId,[FromQuery] long productId)
        {
            if (customerId <= 0 || productId <= 0)
                return BadRequest(ResponseModel.Fail("customerId ve productId zorunludur.", Core.Enums.StatusCode.BadRequest));

            var result = await _productService.GetEffectivePriceAsync(customerId, productId);
            return ToActionResult(result);
        }

        [HttpPost("get-effective-prices")]
        public async Task<IActionResult> GetEffectivePrices([FromBody] CustomerProductRequestDto dto)
        {
            if (dto.CustomerId <= 0 || dto.ProductIds == null || !dto.ProductIds.Any())
                return BadRequest(ResponseModel.Fail("customerId ve productIds zorunludur.", Core.Enums.StatusCode.BadRequest));

            var result = await _productService.GetEffectivePricesAsync(dto);
            return ToActionResult(result);
        }

    }
}