using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Model.Dtos.ServiceType
{
    public class ServiceTypeCreateDto
    {
        [Required(ErrorMessage = "Servis tipi adı zorunludur.")]
        [StringLength(120, MinimumLength = 2, ErrorMessage = "Ad 2-120 karakter olmalıdır.")]
        [NotWhitespace(ErrorMessage = "Ad yalnızca boşluklardan oluşamaz.")]
        public string Name { get; set; } = string.Empty;

        [StringLength(64, MinimumLength = 2, ErrorMessage = "Sözleşme numarası 2-64 karakter olmalıdır.")]
        [RegexIfNotEmpty(@"^[A-Za-z0-9._/-]+$", ErrorMessage = "Sözleşme numarası yalnızca harf, rakam, '.', '_', '-', '/' içerebilir.")]
        public string? ContractNumber { get; set; }
    }

    /// Metin yalnızca boşluklardan oluşamaz.
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public sealed class NotWhitespace : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext _)
        {
            if (value is null) return ValidationResult.Success; // Required ayrı kontrol edilir
            if (value is string s && !string.IsNullOrWhiteSpace(s)) return ValidationResult.Success;
            return new ValidationResult(ErrorMessage ?? "Değer yalnızca boşluk olamaz.");
        }
    }

    /// Boş değilse regex’e uymalı (boş ise geçerli).
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class RegexIfNotEmptyAttribute : ValidationAttribute
    {
        private readonly Regex _regex;
        public RegexIfNotEmptyAttribute(string pattern) =>
            _regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);

        protected override ValidationResult? IsValid(object? value, ValidationContext _)
        {
            if (value is null) return ValidationResult.Success;
            if (value is string s && s.Length == 0) return ValidationResult.Success;
            if (value is string s2 && _regex.IsMatch(s2)) return ValidationResult.Success;
            return new ValidationResult(ErrorMessage ?? "Geçersiz biçim.");
        }
    }
}
