using Business.Interfaces;
using Core.Utilities.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos;
using Model.Dtos.Auth;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly IUserService _userService;

    public AuthController(IAuthService auth, IUserService userService)
    {
        _auth = auth;
        _userService = userService;
    }

    [HttpPost("Login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto loginRequest, CancellationToken cancellationToken)
    {
        var resp = await _auth.LoginAsync(loginRequest, cancellationToken);
        return StatusCode((int)resp.StatusCode, resp.IsSuccess ? resp.Data : resp);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var resp = await _auth.MeAsync();
        return StatusCode((int)resp.StatusCode, resp);
    }

    [HttpPost("ResetPasswordRequest")]
    public async Task<IActionResult> ResetPasswordRequest([FromBody] ResetPasswordRequestDto resetPasswordRequest, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(resetPasswordRequest.Email))
        {
            return BadRequest(Messages.EmailRequired);
        }
        var result = await _userService.ResetPasswordRequestAsync(resetPasswordRequest.Email, cancellationToken);
        return Ok(result);
    }

    [HttpPost("ChangePassword")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto changePasswordDto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(changePasswordDto.RecoveryCode) ||
           string.IsNullOrEmpty(changePasswordDto.NewPasswordConfirm) || string.IsNullOrEmpty(changePasswordDto.NewPassword))
        {
            return BadRequest(Messages.RecoveryCodeOldAndNewPasswordRequired);
        }

        if (changePasswordDto.NewPassword != changePasswordDto.NewPasswordConfirm)
        {
            return BadRequest(Messages.NewPasswordMismatch);
        }

        var result = await _userService.ChangePasswordAsync(changePasswordDto.RecoveryCode, changePasswordDto.NewPassword, cancellationToken);
        return Ok(result);
    }

}
