using Business.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.MenuRole;
using WebAPI.Authorization;

namespace WebAPI.Controllers
{
    [Authorize]
    [MenuResource("RoleListPermission", "UserList")]
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class MenuRolesController : CrudControllerBase<MenuRoleCreateDto, MenuRoleUpdateDto, MenuRoleGetDto, long>
    {
        private readonly IMenuRoleService _menuRoleService;
        public MenuRolesController(
            ICrudService<MenuRoleCreateDto, MenuRoleUpdateDto, MenuRoleGetDto, long> service,
            ILogger<MenuRolesController> logger, IMenuRoleService menuRoleService) : base(service, logger) 
        {
            _menuRoleService = menuRoleService;
        }

        [HttpGet("get-by-role/{roleId:long}")]
        [MenuAuthorize(MenuPermission.View)]
        [Authorize]
        public async Task<IActionResult> GetMyMenusByRole(long roleId)
        {
            var data= await _menuRoleService.GetByRoleIdAsync(roleId);
            
            return Ok(data);
        }

        [HttpGet("get-by-userId/{userId:long}")]
        [MenuAuthorize(MenuPermission.View)]
        [Authorize]
        public async Task<IActionResult> GetMyMenusByUserId(long userId)
        {
            var data = await _menuRoleService.GetByUserIdAsync(userId);

            return Ok(data);
        }
    }


}
