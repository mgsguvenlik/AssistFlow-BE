using Business.Interfaces;
using Core.Common;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.User;
using Model.Requests;
using System.Security.Claims;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    //[Authorize(Roles = "Admin")]
    public class UsersController : CrudControllerBase<UserCreateDto, UserUpdateDto, UserGetDto, long>
    {
        private readonly IUserService _userService;
        private readonly IAuthService _authService;
        public UsersController(
        ICrudService<UserCreateDto, UserUpdateDto, UserGetDto, long> service,
        IUserService userService,
        ILogger<UsersController> logger,
        IAuthService authService)
       : base(service, logger)
        {
            _userService = userService;
            _authService = authService;
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

        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordWithOldRequest req, CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Kullanıcı kimliğini token’dan al (NameIdentifier yoksa "sub" dene)
            var me = await _authService.MeAsync();

            if (me is null || !me.IsSuccess || me.Data is null)
                return Unauthorized("Kullanıcı kimliği bulunamadı.");


            var result = await _userService.ChangePasswordWithOldAsync(
                me.Data.Id,
                req.OldPassword,
                req.NewPassword,
                req.NewPasswordConfirm,
                ct);

            // Service dönüşünü HTTP status’a çevir
            return result.StatusCode switch
            {
                Core.Enums.StatusCode.Ok => Ok(result),
                Core.Enums.StatusCode.Created => StatusCode(StatusCodes.Status201Created, result),
                Core.Enums.StatusCode.BadRequest => BadRequest(result),
                Core.Enums.StatusCode.NotFound => NotFound(result),
                Core.Enums.StatusCode.Conflict => Conflict(result),
                Core.Enums.StatusCode.Unauthorized => Unauthorized(result),
                _ => StatusCode(StatusCodes.Status500InternalServerError, result)
            };
        }


    }
}
