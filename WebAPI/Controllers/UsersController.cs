using Business.Interfaces;
using Core.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.User;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    //[Authorize(Roles = "Admin")]
    public class UsersController : CrudControllerBase<UserCreateDto, UserUpdateDto, UserGetDto, long>
    {
        private readonly IUserService _userService;
        public UsersController(
        ICrudService<UserCreateDto, UserUpdateDto, UserGetDto, long> service,
        IUserService userService,
        ILogger<UsersController> logger)
       : base(service, logger)
        {
            _userService = userService;
        }


        /// POST: api/users/assign-roles
        [HttpPost("assign-roles")]
        [ProducesResponseType(typeof(ResponseModel<UserGetDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AssignRoles([FromBody] AssignUserRolesDto dto)
        {
            var resp = await _userService.AssignRolesAsync(dto.UserId, dto.RoleIds);
            return StatusCode((int)resp.StatusCode, resp);
        }
    }
}
