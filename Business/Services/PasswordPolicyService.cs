using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Business.Interfaces;
using Business.UnitOfWork;
using Core.Settings.Concrete;
using Core.Utilities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Model.Concrete;
using Model.Dtos.Auth;

namespace Business.Services;

public sealed class PasswordPolicyService(IUnitOfWork uow, IOptionsSnapshot<AppSettings> settings) : IPasswordPolicyService
{
    public async Task<PasswordPolicyState?> GetAsync(long userId, CancellationToken ct = default)
    {
        var user = await uow.Repository.GetQueryable<User>().AsNoTracking()
            .Where(x => x.Id == userId && !x.IsDeleted && x.IsActive)
            .Select(x => new { x.Id, x.MustChangePassword, x.PasswordChangedAt, x.PasswordVersion }).FirstOrDefaultAsync(ct);
        if (user is null) return null;
        var value = await uow.Repository.GetQueryable<Configuration>().AsNoTracking()
            .Where(x => x.Name == PasswordPolicyRules.ValidityDaysParameter).Select(x => x.Value).FirstOrDefaultAsync(ct);
        var expires = user.PasswordChangedAt?.AddDays(PasswordPolicyRules.ReadDays(value));
        var expired = !expires.HasValue || expires.Value <= DateTimeOffset.UtcNow;
        return new(user.Id, user.PasswordVersion, user.MustChangePassword || expired,
            user.MustChangePassword ? "Şifrenizi değiştirmeniz gerekiyor." : "Şifrenizin geçerlilik süresi doldu.", expires);
    }

    public string CreateToken(long userId, int version, string purpose, out DateTimeOffset expires)
    {
        expires = DateTimeOffset.UtcNow.AddMinutes(15);
        var config = settings.Value;
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: config.Issuer, audience: config.Audience,
            claims: [new(ClaimTypes.NameIdentifier, userId.ToString()),
                new(PasswordPolicyRules.VersionClaim, version.ToString()), new(PasswordPolicyRules.PurposeClaim, purpose)],
            expires: expires.UtcDateTime,
            signingCredentials: new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config.Key)), SecurityAlgorithms.HmacSha256)));
    }

    public PasswordTokenIdentity? ValidateToken(string token, string purpose)
    {
        try
        {
            var config = settings.Value;
            var principal = new JwtSecurityTokenHandler().ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = config.Issuer,
                ValidateAudience = true, ValidAudience = config.Audience,
                ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config.Key)),
                ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true,
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256], ClockSkew = TimeSpan.Zero
            }, out _);
            if (principal.FindFirstValue(PasswordPolicyRules.PurposeClaim) != purpose ||
                !long.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) || id <= 0 ||
                !int.TryParse(principal.FindFirstValue(PasswordPolicyRules.VersionClaim), out var version)) return null;
            return new(id, version);
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException) { return null; }
    }
}
