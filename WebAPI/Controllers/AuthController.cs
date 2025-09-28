using Business.Interfaces;
using Core.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.Auth;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(ResponseModel<LoginResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
    {
        var resp = await _auth.LoginAsync(dto);
        return StatusCode((int)resp.StatusCode, resp);
    }

    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(typeof(ResponseModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout()
    {
        var resp = await _auth.LogoutAsync();
        return StatusCode((int)resp.StatusCode, resp);
    }

    // Opsiyonel: mevcut oturum bilgisi
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(ResponseModel<CurrentUserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Me()
    {
        var resp = await _auth.MeAsync();
        return StatusCode((int)resp.StatusCode, resp);
    }
}
