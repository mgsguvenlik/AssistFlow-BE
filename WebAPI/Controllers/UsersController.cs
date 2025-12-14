using Business.Interfaces;
using Core.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.User;
using Model.Dtos.WorkFlowDtos.WorkFlowActivityRecord;
using Model.Requests;

namespace WebAPI.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : CrudControllerBase<UserCreateDto, UserUpdateDto, UserGetDto, long>
    {
        private readonly IUserService _userService;
        private readonly IAuthService _authService;
        private readonly IActivationRecordService _activationRecordService;
        public UsersController(
        ICrudService<UserCreateDto, UserUpdateDto, UserGetDto, long> service,
        IUserService userService,
        ILogger<UsersController> logger,
        IAuthService authService,
        IActivationRecordService activationRecordService)
       : base(service, logger)
        {
            _userService = userService;
            _authService = authService;
            _activationRecordService = activationRecordService;
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


        [HttpGet("by-role/{roleId:long}")]
        public async Task<IActionResult> GetUsersByRole(long roleId)
        {
            if (roleId <= 0)
                return BadRequest("Geçersiz rol kimliği.");

            var resp = await _userService.GetUserByRoleAsync(roleId);

            if (resp is null)
                return StatusCode(StatusCodes.Status500InternalServerError, "Servis yanıtı null döndü.");

            return StatusCode((int)resp.StatusCode, resp);
        }



        [HttpGet("technicians")]
        public async Task<IActionResult> GetTechnicians()
        {
            var resp = await _userService.GetTechniciansAsync();

            if (resp is null)
                return StatusCode(StatusCodes.Status500InternalServerError, "Servis yanıtı null döndü.");

            return StatusCode((int)resp.StatusCode, resp);
        }


        [HttpGet("get-user-activity/{userId}")]
        public async Task<IActionResult> GetUserActivityRecords([FromRoute] int userId, [FromQuery] QueryParams q)
        {
            var result = await _activationRecordService.GetUserActivity(userId, q);
            return ToActionResult(result);
        }


        [HttpGet("get-user-activity-grouped/{userId:int}")]
        [ProducesResponseType(typeof(ResponseModel<PagedResult<WorkFlowActivityGroupDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetUserActivityGroupedByRequestNo([FromRoute] int userId, [FromQuery] QueryParams q, [FromQuery] int perGroupTake = 50)
        {
            if (userId <= 0)
                return BadRequest("Geçersiz kullanıcı kimliği.");

            var result = await _activationRecordService
                .GetUserActivityGroupedByRequestNo(userId, q, perGroupTake);

            return ToActionResult(result);
        }



        [HttpPost("update-user-password")]
        [ProducesResponseType(typeof(ResponseModel<UserGetDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateUserPassword([FromBody] UpdateUserPasswordDto req, CancellationToken ct)
        {
            if (req is null)
                return BadRequest("İstek boş olamaz.");

            if (req.UserId <= 0)
                return BadRequest("Geçersiz kullanıcı kimliği.");

            if (string.IsNullOrWhiteSpace(req.NewPassword))
                return BadRequest("Yeni şifre boş olamaz.");

            var result = await _userService.UpdateUserPassword(req.UserId, req.NewPassword, ct);

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
