using Business.Interfaces;
using Core.Common;
using Core.Settings.Concrete;
using Core.Utilities.Constants;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Model.Dtos.Auth;
using Model.Dtos.Menu;
using Model.Dtos.Role;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

public class AuthService : IAuthService
{
    private readonly IHttpContextAccessor _http;
    private readonly IUserService _userService;
    private readonly IOptionsSnapshot<AppSettings> _appSettings;

    private readonly IMenuService _menuService;
    public AuthService(
        IHttpContextAccessor http,
        IUserService userService,
        IOptionsSnapshot<AppSettings> appSettings
,
        IMenuService menuService)
    {
        _http = http;
        _userService = userService;
        _appSettings = appSettings;
        _menuService = menuService;
    }
    public async Task<ResponseModel<AuthResponseDto>> LoginAsync(LoginRequestDto loginRequest, CancellationToken ct = default)
    {
        // 1) Kullanıcı doğrulama
        var result = await _userService.SignInAsync(loginRequest.Identifier, loginRequest.Password);
        if (!result.IsSuccess || result.Data == null || !result.Data.IsActive)
            return ResponseModel<AuthResponseDto>.Fail(Messages.Unauthorized, Core.Enums.StatusCode.Unauthorized);

        var user = result.Data;

        // 2) Menüleri çek (user.Id ile)
        var menus = await _menuService.GetByUserIdAsync(user.Id);

        // 3) Token üret
        var issuer = _appSettings.Value.Issuer;
        var audience = _appSettings.Value.Audience;
        var key = _appSettings.Value.Key;

        var minutes = _appSettings.Value.AccessTokenMinutes > 0
            ? _appSettings.Value.AccessTokenMinutes
            : 60;

        if (loginRequest.RememberMe)
            minutes = 15000;

        var claims = new List<Claim>
           {
               new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
               new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
               new Claim(ClaimTypes.Name, user.TechnicianName ?? string.Empty),
               new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
           };

        if (!string.IsNullOrWhiteSpace(user.TechnicianEmail))
            claims.Add(new Claim(ClaimTypes.Email, user.TechnicianEmail));

        // 🔹 TENANT CLAIM'LERİ
        if (user.TenantId > 0)
            claims.Add(new Claim("tenant_id", user.TenantId.ToString()));

        if (!string.IsNullOrWhiteSpace(user.TenantCode))
            claims.Add(new Claim("tenant_code", user.TenantCode));

        if (!string.IsNullOrWhiteSpace(user.TenantName))
            claims.Add(new Claim("tenant_name", user.TenantName));


        // ROLLER
        if (user.Roles != null)
        {
            foreach (var role in user.Roles)
            {
                var roleName = role?.Name ?? role?.ToString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(roleName))
                    claims.Add(new Claim(ClaimTypes.Role, roleName));
            }
        }

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var creds = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.Now.AddMinutes(minutes);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expires,
            signingCredentials: creds
        );
        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        // 4) Cevap DTO
        var dto = new AuthResponseDto
        {
            Token = tokenString,
            Expires = expires,
            User = user,
            Status = 200,
            Menus = menus?.ToList() ?? new List<MenuWithPermissionsDto>()
        };

        return ResponseModel<AuthResponseDto>.Success(dto);
    }
    public async Task<ResponseModel<CurrentUserDto>> MeAsync()
    {
        var p = _http.HttpContext?.User;
        var isAuth = p?.Identity?.IsAuthenticated ?? false;

        if (!isAuth)
            return ResponseModel<CurrentUserDto>.Success(new CurrentUserDto { IsAuthenticated = false });

        // 1) Claim'den Id
        var idStr = p?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                 ?? p?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        if (!long.TryParse(idStr, out var userId) || userId <= 0)
            return ResponseModel<CurrentUserDto>.Fail(Messages.UserIdClaimNotFound, Core.Enums.StatusCode.Unauthorized);

        // 2) Back-end'den kullanıcıyı getir
        var userRes = await _userService.GetByIdAsync(userId);
        if (!userRes.IsSuccess || userRes.Data is null)
            return ResponseModel<CurrentUserDto>.Fail(Messages.UserNotFound, Core.Enums.StatusCode.NotFound);

        var u = userRes.Data;

        // 🔹 Tenant claim'lerini de oku
        long? tenantIdFromToken = null;
        var tenantIdStr = p?.FindFirst("tenant_id")?.Value;
        if (long.TryParse(tenantIdStr, out var tid) && tid > 0)
            tenantIdFromToken = tid;

        var tenantCodeFromToken = p?.FindFirst("tenant_code")?.Value;
        var tenantNameFromToken = p?.FindFirst("tenant_name")?.Value;

        // 4) DTO’yu doldur
        var dto = new CurrentUserDto
        {
            IsAuthenticated = true,
            Id = u.Id,
            Name = string.IsNullOrWhiteSpace(u.TechnicianName)
                                    ? (p?.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty)
                                    : u.TechnicianName,
            Email = string.IsNullOrWhiteSpace(u.TechnicianEmail)
                                    ? p?.FindFirst(ClaimTypes.Email)?.Value
                                    : u.TechnicianEmail,
            TechnicianCode = u.TechnicianCode ?? string.Empty,
            TechnicianCompany = u.TechnicianCompany,
            TechnicianAddress = u.TechnicianAddress,
            City = u.City,
            District = u.District,
            TechnicianName = u.TechnicianName ?? string.Empty,
            TechnicianPhone = u.TechnicianPhone,
            TechnicianEmail = u.TechnicianEmail,
            Roles = u.Roles ?? new List<RoleGetDto>(),

            // 🔹 Tenant bilgisi: önce DB, yoksa token claim’inden
            TenantId = (u.TenantId > 0 ? u.TenantId : tenantIdFromToken),
            TenantCode = !string.IsNullOrWhiteSpace(u.TenantCode) ? u.TenantCode : tenantCodeFromToken,
            TenantName = !string.IsNullOrWhiteSpace(u.TenantName) ? u.TenantName : tenantNameFromToken
        };

        return ResponseModel<CurrentUserDto>.Success(dto);
    }

}
