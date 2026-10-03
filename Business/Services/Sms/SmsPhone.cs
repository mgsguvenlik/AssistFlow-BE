namespace Business.Services.Sms;

public static class SmsPhone
{
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (value.Any(c => !char.IsAsciiDigit(c) && !char.IsWhiteSpace(c) && c is not ('+' or '-' or '(' or ')')))
            return null;
        var digits = new string(value.Where(char.IsAsciiDigit).ToArray());
        if (digits.StartsWith("0090", StringComparison.Ordinal)) digits = digits[4..];
        else if (digits.Length == 12 && digits.StartsWith("90", StringComparison.Ordinal)) digits = digits[2..];
        else if (digits.Length == 11 && digits[0] == '0') digits = digits[1..];
        return digits.Length == 10 && digits[0] == '5' ? "90" + digits : null;
    }

    public static string? Select(string? phone1, string? phone2) => Normalize(phone1) ?? Normalize(phone2);
}
