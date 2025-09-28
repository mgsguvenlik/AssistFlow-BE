using Business.Interfaces;
using Business.UnitOfWork;
using Core.Common;
using Core.Enums;
using Mapster;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Model.Concrete;
using Model.Dtos.Auth;
using System.Security.Claims;
using System.Text.Json;

public class AuthService : IAuthService
{
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher<User> _hasher;
    private readonly IHttpContextAccessor _http;
    private readonly TypeAdapterConfig _config;

    public AuthService(IUnitOfWork uow,
                       IPasswordHasher<User> hasher,
                       IHttpContextAccessor http,
                       TypeAdapterConfig config)
    {
        _uow = uow;
        _hasher = hasher;
        _http = http;
        _config = config;
    }

    private static Func<IQueryable<User>, IIncludableQueryable<User, object>> Includes()
        => q => q.Include(u => u.UserRoles).ThenInclude(ur => ur.Role);

    public async Task<ResponseModel<LoginResponseDto>> LoginAsync(LoginRequestDto dto)
    {
        try
        {
            var user = await _uow.Repository.GetSingleAsync<User>(
                asNoTracking: false,
                whereExpression: u =>
                    (u.TechnicianEmail != null && u.TechnicianEmail == dto.Identifier) ||
                    (u.TechnicianCode != null && u.TechnicianCode == dto.Identifier),
                includeExpression: Includes());

            if (user is null)
                return ResponseModel<LoginResponseDto>.Fail("Kullanıcı bulunamadı.", StatusCode.NotFound);

            var vr = _hasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password);
            if (vr == PasswordVerificationResult.Failed)
                return ResponseModel<LoginResponseDto>.Fail("Kullanıcı adı/şifre hatalı.", StatusCode.BadRequest);

            // claim’ler
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.TechnicianName),
                new(ClaimTypes.Email, user.TechnicianEmail ?? "")
            };
            var roleNames = user.UserRoles
                                .Where(ur => ur.Role != null && ur.Role.Name != null)
                                .Select(ur => ur.Role!.Name!)
                                .Distinct()
                                .ToList();
            foreach (var rn in roleNames)
                claims.Add(new Claim(ClaimTypes.Role, rn));

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            var authProps = new AuthenticationProperties
            {
                IsPersistent = dto.RememberMe,
                ExpiresUtc = dto.RememberMe
                    ? DateTimeOffset.Now.AddDays(7)
                    : DateTimeOffset.Now.AddHours(8),
                AllowRefresh = true
            };

            await _http.HttpContext!.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                authProps);

            // Session doldur
            var session = _http.HttpContext!.Session;
            session.SetString("Id", user.Id.ToString());
            session.SetString("Name", user.TechnicianName);
            session.SetString("Email", user.TechnicianEmail ?? "");
            session.SetString("Roles", JsonSerializer.Serialize(roleNames));

            var resp = new LoginResponseDto
            {
                Id = user.Id,
                Name = user.TechnicianName,
                Email = user.TechnicianEmail,
                Roles = roleNames
            };
            return ResponseModel<LoginResponseDto>.Success(resp, "Giriş başarılı.");
        }
        catch (Exception ex)
        {
            return ResponseModel<LoginResponseDto>.Fail($"Beklenmeyen hata: {ex.Message}", StatusCode.Error);
        }
    }

    public async Task<ResponseModel> LogoutAsync()
    {
        try
        {
            await _http.HttpContext!.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            _http.HttpContext!.Session.Clear();
            return ResponseModel.Success("Çıkış yapıldı.", StatusCode.Ok);
        }
        catch (Exception ex)
        {
            return ResponseModel.Fail($"Beklenmeyen hata: {ex.Message}", StatusCode.Error);
        }
    }

    public Task<ResponseModel<CurrentUserDto>> MeAsync()
    {
        var user = _http.HttpContext?.User;
        var sess = _http.HttpContext?.Session;
        var dto = new CurrentUserDto { IsAuthenticated = user?.Identity?.IsAuthenticated ?? false };

        if (dto.IsAuthenticated && sess is not null)
        {
            dto.Id = long.TryParse(sess.GetString("Id"), out var id) ? id : 0;
            dto.Name = sess.GetString("Name") ?? "";
            dto.Email = sess.GetString("Email");
            var rolesJson = sess.GetString("Roles");
            dto.Roles = string.IsNullOrWhiteSpace(rolesJson)
                ? new List<string>()
                : (JsonSerializer.Deserialize<List<string>>(rolesJson) ?? new List<string>());
        }

        return Task.FromResult(ResponseModel<CurrentUserDto>.Success(dto));
    }
}
