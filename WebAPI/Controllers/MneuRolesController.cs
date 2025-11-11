using Business.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.MenuRole;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class MneuRolesController : CrudControllerBase<MenuRoleCreateDto, MenuRoleUpdateDto, MenuRoleGetDto, long>
    {
        public MneuRolesController(
            ICrudService<MenuRoleCreateDto, MenuRoleUpdateDto, MenuRoleGetDto, long> service,
            ILogger<MneuRolesController> logger) : base(service, logger) { }
    }
}
