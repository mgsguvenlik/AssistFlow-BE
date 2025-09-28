// WebAPI/Controllers/ProductTypesController.cs
using Business.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Model.Dtos.ProductType;

namespace WebAPI.Controllers
{
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
