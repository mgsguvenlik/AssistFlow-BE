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

        var appSettings = ServiceTool.ServiceProvider.GetService<IOptionsSnapshot<AppSettings>>();
        var issuer = appSettings.Value.Issuer;
        var audience = appSettings.Value.Audience;
        var key = appSettings.Value.Key;
        var result = await _userService.SignInAsync(loginRequest.Identifier, loginRequest.Password);
        if (!result.IsSuccess || result.Data == null)
            return Unauthorized(result);

        var user = result.Data;

        // JWT Token generation
        var claims = new List<System.Security.Claims.Claim>
            {
                new(System.Security.Claims.ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(System.Security.Claims.ClaimTypes.Name, user.TechnicianEmail ?? string.Empty),
                new(System.Security.Claims.ClaimTypes.Role,string.Join(",",user.Roles) ?? string.Empty),
                //new("email", user.Email ?? string.Empty)
            };

        if (user.Roles != null)
        {
            foreach (var role in user.Roles)
            {
                claims.Add(new(System.Security.Claims.ClaimTypes.Role, role.Name ?? string.Empty));
            }
        }
        var keyEncode = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(key));
        var creds = new Microsoft.IdentityModel.Tokens.SigningCredentials(keyEncode, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);

        var expires = DateTime.UtcNow.AddHours(1);

        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expires,
            signingCredentials: creds
        );

        var tokenString = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
        //return Ok(tokenString);
        return Ok(new
        {
            Token = tokenString,
            Status = 200,
            Expires = expires,
            Email = user.TechnicianEmail,
            Name = user.TechnicianName,
            UserId = user.Id,
            Roles = user.Roles.ToList()
        });
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
