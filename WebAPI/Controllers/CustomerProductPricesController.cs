using Business.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.Brand;
using Model.Dtos.CustomerProductPrice;

namespace WebAPI.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class CustomerProductPricesController : CrudControllerBase<CustomerProductPriceCreateDto, CustomerProductPriceUpdateDto, CustomerProductPriceGetDto, long>
    {
        public CustomerProductPricesController(
            ICrudService<CustomerProductPriceCreateDto, CustomerProductPriceUpdateDto, CustomerProductPriceGetDto, long> service,
            ILogger<CustomerProductPricesController> logger) : base(service, logger) { }
    }
}
