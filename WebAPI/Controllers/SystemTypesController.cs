using Business.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.SystemType;

namespace WebAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class SystemTypesController
        : CrudControllerBase<SystemTypeCreateDto, SystemTypeUpdateDto, SystemTypeGetDto, long>
    {
        public SystemTypesController(
            ICrudService<SystemTypeCreateDto, SystemTypeUpdateDto, SystemTypeGetDto, long> service,
            ILogger<SystemTypesController> logger) : base(service, logger) { }
    }
}
