using Model.Concrete;
using Model.Dtos.Auth;

namespace Business.Interfaces;

public interface IPasswordPolicyService
{
    Task<PasswordPolicyState?> GetAsync(long userId, CancellationToken ct = default);
    string CreateToken(long userId, int version, string purpose, out DateTimeOffset expires);
    PasswordTokenIdentity? ValidateToken(string token, string purpose);
}

public interface IPasswordPolicyNotifier
{
    Task NotifyAsync(long userId, CancellationToken ct = default);
}
