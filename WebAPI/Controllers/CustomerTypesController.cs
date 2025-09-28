using Business.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.CustomerType;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class CustomerTypesController
        : CrudControllerBase<CustomerTypeCreateDto, CustomerTypeUpdateDto, CustomerTypeGetDto, long>
    {
        public CustomerTypesController(
            ICrudService<CustomerTypeCreateDto, CustomerTypeUpdateDto, CustomerTypeGetDto, long> service,
            ILogger<CustomerTypesController> logger)
            : base(service, logger) { }
    }
}
