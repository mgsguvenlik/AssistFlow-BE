using Core.Utilities.Constants;
using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.Role
{
    public class RoleCreateDto
    {
        [Required(ErrorMessage = Messages.RoleNameRequired)]
        [StringLength(64, MinimumLength = 2, ErrorMessage = Messages.RoleNameLength)]
        [NotWhitespace(ErrorMessage = Messages.RoleNameCannotBeWhitespace)]
        public string Name { get; set; } = string.Empty;

        [StringLength(32, MinimumLength = 2, ErrorMessage = Messages.CodeLength)]
        [RegularExpression(@"^[A-Z0-9._-]+$", ErrorMessage = Messages.CodeInvalidChars)]
        public string? Code { get; set; }
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
