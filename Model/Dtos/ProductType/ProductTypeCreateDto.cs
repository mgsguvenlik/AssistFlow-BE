using Core.Utilities.Constants;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Model.Dtos.ProductType
{
    public class ProductTypeCreateDto
    {
        [Required(ErrorMessage = Messages.ProductTypeNameRequired)]
        [StringLength(120, MinimumLength = 2, ErrorMessage = Messages.ProductTypeNameLength)]
        [NotWhitespace(ErrorMessage = Messages.ProductTypeNameCannotBeWhitespace)]
        public string Type { get; set; } = string.Empty;

        [StringLength(32, MinimumLength = 2, ErrorMessage = Messages.CodeLength)]
        [RegexIfNotEmpty(@"^[A-Z0-9._-]+$", ErrorMessage = Messages.CodeInvalidChars)]
        public string? Code { get; set; }
    }

    /// Metin sadece boşluklardan oluşamaz.
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public sealed class NotWhitespace : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext _)
        {
            if (value is null) return ValidationResult.Success; // Required ayrı kontrol edilir
            if (value is string s && !string.IsNullOrWhiteSpace(s)) return ValidationResult.Success;
            return new ValidationResult(ErrorMessage ?? Messages.ValueCannotBeWhitespace);
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
            return new ValidationResult(ErrorMessage ?? Messages.InvalidFormat);
        }
    }
}
