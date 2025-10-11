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
        public CustomerGroupProductPricesController(
            ICrudService<CustomerGroupProductPriceCreateDto, CustomerGroupProductPriceUpdateDto, CustomerGroupProductPriceGetDto, long> service,
            ILogger<CustomerGroupProductPricesController> logger) : base(service, logger) { }
    }
}
