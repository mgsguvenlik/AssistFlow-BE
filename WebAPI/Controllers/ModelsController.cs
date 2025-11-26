using Business.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.Model;

namespace WebAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class ModelsController
        : CrudControllerBase<ModelCreateDto, ModelUpdateDto, ModelGetDto, long>
    {
        public ModelsController(
            ICrudService<ModelCreateDto, ModelUpdateDto, ModelGetDto, long> service,
            ILogger<ModelsController> logger) : base(service, logger) { }
    }
}
