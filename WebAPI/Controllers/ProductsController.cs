// WebAPI/Controllers/ProductsController.cs
using Business.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Model.Dtos.Product;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class ProductsController
        : CrudControllerBase<ProductCreateDto, ProductUpdateDto, ProductGetDto, long>
    {
        public ProductsController(
            ICrudService<ProductCreateDto, ProductUpdateDto, ProductGetDto, long> service,
            ILogger<ProductsController> logger) : base(service, logger) { }
    }
}
