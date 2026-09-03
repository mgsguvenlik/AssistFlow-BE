using Business.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.MenuRole;
using WebAPI.Authorization;

namespace WebAPI.Controllers
{
    [Authorize]
    [MenuResource("RoleListPermission", "UserList", "RoleList")]
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

        /// <summary>POST /api/MenuRoles -> Create (also reachable from RoleList's permission matrix)</summary>
        [HttpPost]
        [MenuAuthorize(new[] { "RoleListPermission", "RoleList" }, MenuPermission.Edit)]
        public override async Task<IActionResult> Create([FromBody] MenuRoleCreateDto dto)
        {
            return await base.Create(dto);
        }

        /// <summary>POST /api/MenuRoles/update -> Update (also reachable from RoleList's permission matrix)</summary>
        [HttpPost("update")]
        [MenuAuthorize(new[] { "RoleListPermission", "RoleList" }, MenuPermission.Edit)]
        public override async Task<IActionResult> Update([FromBody] MenuRoleUpdateDto dto)
        {
            return await base.Update(dto);
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
        [MenuAuthorize(new[] { "UserList", "UserDetail" }, MenuPermission.View)]
        [Authorize]
        public async Task<IActionResult> GetMyMenusByUserId(long userId)
        {
            var data = await _menuRoleService.GetByUserIdAsync(userId);

            return Ok(data);
        }
    }


}
