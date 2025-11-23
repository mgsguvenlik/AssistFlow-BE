using Business.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.CustomerSystem;

namespace WebAPI.Controllers
{
    //[Authorize]
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class CustomerSystemsController
       : CrudControllerBase<CustomerSystemCreateDto, CustomerSystemUpdateDto, CustomerSystemGetDto, long>
    {
        public CustomerSystemsController(
            ICrudService<CustomerSystemCreateDto, CustomerSystemUpdateDto, CustomerSystemGetDto, long> service,
            ILogger<CustomerSystemsController> logger)
            : base(service, logger)
        {
        }
    }
}
