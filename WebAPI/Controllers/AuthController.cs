using Business.Interfaces;
using Core.Common;
using Core.Settings.Concrete;
using Core.Utilities.IoC;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
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
}
