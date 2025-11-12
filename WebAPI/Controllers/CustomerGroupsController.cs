using Business.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.CustomerGroup;

namespace WebAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class CustomerGroupsController
        : CrudControllerBase<CustomerGroupCreateDto, CustomerGroupUpdateDto, CustomerGroupGetDto, long>
    {
        public CustomerGroupsController(
            ICrudService<CustomerGroupCreateDto, CustomerGroupUpdateDto, CustomerGroupGetDto, long> service,
            ILogger<CustomerGroupsController> logger)
            : base(service, logger) { }
    }
}
