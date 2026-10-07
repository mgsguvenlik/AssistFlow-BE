using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.Auth;

public sealed class RequiredPasswordChangeDto
{
    [Required] public string PasswordChangeToken { get; set; } = string.Empty;
    [Required] public string NewPassword { get; set; } = string.Empty;
    [Required] public string NewPasswordConfirm { get; set; } = string.Empty;
}

public sealed record PasswordPolicyState(long UserId, int Version, bool RequiresChange, string Reason, DateTimeOffset? ExpiresAt);
public sealed record PasswordTokenIdentity(long UserId, int Version);
