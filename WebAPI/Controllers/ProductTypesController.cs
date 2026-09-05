using Business.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Authorization;
using Model.Dtos.ProductType;

namespace WebAPI.Controllers
{
    [Authorize]
    [MenuResource("ProductTypeList", "ProductList", "ServiceRequestCreate", "YkbServiceRequestCreate", "QnbServiceRequestCreate")]
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class ProductTypesController
        : CrudControllerBase<ProductTypeCreateDto, ProductTypeUpdateDto, ProductTypeGetDto, long>
    {
        public ProductTypesController(
            ICrudService<ProductTypeCreateDto, ProductTypeUpdateDto, ProductTypeGetDto, long> service,
            ILogger<ProductTypesController> logger) : base(service, logger) { }
    }
}
