using Business.Interfaces;
using Core.Utilities.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.Menu;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class MenusController : CrudControllerBase<MenuCreateDto, MenuUpdateDto, MenuGetDto, long>
    {

        private readonly IAuthService _authService;
        private readonly IMenuService _menuService;
        public MenusController(
            ICrudService<MenuCreateDto, MenuUpdateDto, MenuGetDto, long> service,
            ILogger<MenusController> logger,
            IAuthService authService,
            IMenuService menuService) : base(service, logger)
        {
            _authService = authService;
            _menuService = menuService;
        }

        [HttpGet("my")]
        [Authorize]
        public async Task<IActionResult> GetMyMenus(CancellationToken ct)
        {
            var me = await _authService.MeAsync();
            if (!me.IsSuccess || me.Data is null || !me.Data.IsAuthenticated)
                return Unauthorized(Messages.Unauthorized);

            var menus = await _menuService.GetByUserIdAsync(me.Data.Id);
            return Ok(menus);
        }
    }
}
