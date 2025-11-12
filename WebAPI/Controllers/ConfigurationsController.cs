using Business.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.Configuration;

namespace WebAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class ConfigurationsController : CrudControllerBase<ConfigurationCreateDto, ConfigurationUpdateDto, ConfigurationGetDto, long>
    {
        public ConfigurationsController(
            ICrudService<ConfigurationCreateDto, ConfigurationUpdateDto, ConfigurationGetDto, long> service,
            ILogger<ConfigurationsController> logger) : base(service, logger) { }
    }
}
