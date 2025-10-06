using Core.Utilities.Constants;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Model.Dtos.SystemType
{
    public class SystemTypeCreateDto
    {
        [Required(ErrorMessage = Messages.SystemTypeNameRequired)]
        [StringLength(120, MinimumLength = 2, ErrorMessage = Messages.NameLength)]
        [NotWhitespace(ErrorMessage = Messages.NameCannotBeWhitespace)]
        public string Name { get; set; } = string.Empty;

        [StringLength(32, MinimumLength = 2, ErrorMessage = Messages.CodeLength)]
        [RegexIfNotEmpty(@"^[A-Z0-9._-]+$", ErrorMessage = Messages.CodeInvalidChars)]
        public string? Code { get; set; }
    }

    /// Metin yalnızca boşluklardan oluşamaz.
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
