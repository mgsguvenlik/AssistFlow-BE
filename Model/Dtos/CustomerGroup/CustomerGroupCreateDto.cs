using Core.Utilities.Constants;
using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.CustomerGroup
{
    public class CustomerGroupCreateDto
    {
        [Required(ErrorMessage = Messages.GroupNameRequired)]
        [StringLength(120, MinimumLength = 2, ErrorMessage = Messages.GroupNameLength)]
        [NotWhitespace(ErrorMessage = Messages.GroupNameCannotBeWhitespace)]
        public string GroupName { get; set; } = string.Empty;

        [Required(ErrorMessage = Messages.CodeRequired)]
        [StringLength(32, MinimumLength = 2, ErrorMessage = Messages.CodeLength)]
        [RegularExpression(@"^[A-Z0-9._-]+$", ErrorMessage = Messages.CodeInvalidChars)]
        public string Code { get; set; } = string.Empty;
    }

    /// <summary>Metin sadece boşluklardan oluşamaz.</summary>
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
