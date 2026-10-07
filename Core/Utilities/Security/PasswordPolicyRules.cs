namespace Core.Utilities.Security;

public static class PasswordPolicyRules
{
    public const string ValidityDaysParameter = "PasswordValidityDays";
    public const int DefaultValidityDays = 180;
    public const string VersionClaim = "password_version";
    public const string PurposeClaim = "token_use";
    public const string AccessPurpose = "access";
    public const string ChangePurpose = "password-change";
    public const string ResetPurpose = "password-reset";

    public static bool ValidDays(string? value) => int.TryParse(value, out var days) && days is > 0 and <= 36500;
    public static int ReadDays(string? value) => ValidDays(value) ? int.Parse(value!) : DefaultValidityDays;
    public static string? ValidatePassword(string password)
    {
        if (password.Length < 8) return "Şifre en az 8 karakter olmalıdır.";
        if (!password.Any(char.IsDigit)) return "Şifre en az bir rakam içermelidir.";
        if (!password.Any(char.IsLower)) return "Şifre en az bir küçük harf içermelidir.";
        if (!password.Any(char.IsUpper)) return "Şifre en az bir büyük harf içermelidir.";
        return null;
    }
}
