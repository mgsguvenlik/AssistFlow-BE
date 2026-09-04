using Business.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Authorization;
using Model.Dtos.Role;

namespace WebAPI.Controllers
{
    [Authorize]
    [MenuResource("RoleList", "UserList")]
    [Route("api/[controller]")]
    [ApiController]
    public class RolesController
        : CrudControllerBase<RoleCreateDto, RoleUpdateDto, RoleGetDto, long>
    {
        public RolesController(
            ICrudService<RoleCreateDto, RoleUpdateDto, RoleGetDto, long> service,
            ILogger<RolesController> logger)
            : base(service, logger)
        {
        }

    }
}
