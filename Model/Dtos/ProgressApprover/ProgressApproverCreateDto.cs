using Core.Utilities.Constants;
using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.ProgressApprover
{
    public class ProgressApproverCreateDto
    {
        [Required(ErrorMessage = Messages.FullNameRequired)]
        [StringLength(120, MinimumLength = 2, ErrorMessage = Messages.FullNameLength)]
        [NotWhitespace(ErrorMessage = Messages.FullNameCannotBeWhitespace)]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = Messages.EmailRequired)]
        [EmailAddress(ErrorMessage = Messages.EnterValidEmail)]
        [StringLength(200, ErrorMessage = Messages.EmailMaxLength)]
        public string Email { get; set; } = string.Empty;

        [Range(1, long.MaxValue, ErrorMessage = Messages.SelectValidCustomer)]
        public long CustomerGroupId { get; set; }

        [Required]
        public string Phone { get; set; } = string.Empty;
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
