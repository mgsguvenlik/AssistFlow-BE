using Business.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.MenuRole;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class MenuRolesController : CrudControllerBase<MenuRoleCreateDto, MenuRoleUpdateDto, MenuRoleGetDto, long>
    {
        public MenuRolesController(
            ICrudService<MenuRoleCreateDto, MenuRoleUpdateDto, MenuRoleGetDto, long> service,
            ILogger<MenuRolesController> logger) : base(service, logger) { }
    }
}
