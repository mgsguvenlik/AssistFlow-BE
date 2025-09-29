using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Model.Dtos.Auth
{
    public class LoginRequestDto
    {
        [Required(ErrorMessage = "Kullanıcı adı (e-posta veya teknisyen kodu) zorunludur.")]
        public string Identifier { get; set; } = string.Empty; // email veya TechnicianCode

        [Required(ErrorMessage = "Şifre zorunludur.")]
        public string Password { get; set; } = string.Empty;

        // bool için extra validasyona gerek yok; default true.
        public bool RememberMe { get; set; } = true;
    }

    /// <summary>
    /// Identifier eğer '@' içeriyorsa e-posta formatını, aksi halde teknisyen kodu formatını doğrular.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public sealed class EmailOrTechnicianCodeAttribute : ValidationAttribute
    {
        public int MinCodeLength { get; set; } = 3;
        public int MaxCodeLength { get; set; } = 32;

        // Kod için izin verilen karakterler: harf, rakam, nokta, alt çizgi, tire
        private static readonly Regex CodeRegex = new(@"^[A-Za-z0-9._-]+$", RegexOptions.Compiled);

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            var s = (value as string)?.Trim();
            if (string.IsNullOrWhiteSpace(s))
                return new ValidationResult(ErrorMessage ?? "Kullanıcı adı zorunludur.");

            if (s.Contains('@'))
            {
                var emailAttr = new EmailAddressAttribute();
                if (emailAttr.IsValid(s))
                    return ValidationResult.Success;

                return new ValidationResult(ErrorMessage ?? "Geçerli bir e-posta adresi girin.");
            }

            if (s.Length < MinCodeLength || s.Length > MaxCodeLength)
                return new ValidationResult(ErrorMessage ?? $"Teknisyen kodu {MinCodeLength}-{MaxCodeLength} karakter olmalıdır.");

            if (!CodeRegex.IsMatch(s))
                return new ValidationResult(ErrorMessage ?? "Teknisyen kodu yalnızca harf, rakam, '.', '_' ve '-' içerebilir.");

            return ValidationResult.Success;
        }
    }
}
