using Business.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.MenuRole;

namespace WebAPI.Controllers
{
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
        [Authorize]
        public async Task<IActionResult> GetMyMenusByRole(long roleId)
        {
            var data= await _menuRoleService.GetByRoleIdAsync(roleId);
            
            return Ok(data);
        }
    }


}
