using Core.Utilities.Constants;
using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.CurrencyType
{
    public class CurrencyTypeCreateDto
    {
        [Required(ErrorMessage = Messages.CurrencyCodeRequired)]
        [RegularExpression(@"^[A-Z]{3}$",
            ErrorMessage = Messages.CurrencyCodeFormat)]
        public string Code { get; set; } = string.Empty;

        [StringLength(120, ErrorMessage = Messages.NameMaxLength)]
        [NotWhitespace(ErrorMessage = Messages.NameCannotBeWhitespace)]
        public string? Name { get; set; }
    }

    /// <summary>Metin yalnızca boşluklardan oluşamaz.</summary>
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
}
