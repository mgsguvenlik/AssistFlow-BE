using Business.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.ServiceType;

namespace WebAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class ServiceTypesController
        : CrudControllerBase<ServiceTypeCreateDto, ServiceTypeUpdateDto, ServiceTypeGetDto, long>
    {
        public ServiceTypesController(
            ICrudService<ServiceTypeCreateDto, ServiceTypeUpdateDto, ServiceTypeGetDto, long> service,
            ILogger<ServiceTypesController> logger) : base(service, logger) { }
    }
}
