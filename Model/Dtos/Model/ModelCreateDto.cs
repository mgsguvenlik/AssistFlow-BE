using Core.Utilities.Constants;
using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.Model
{
    public class ModelCreateDto
    {
        [Required(ErrorMessage = Messages.ModelNameRequired)]
        [StringLength(120, MinimumLength = 2, ErrorMessage = Messages.ModelNameLength)]
        [NotWhitespace(ErrorMessage = Messages.ModelNameCannotBeWhitespace)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = Messages.DescriptionMaxLength)]
        public string? Desc { get; set; }

        [Range(1, long.MaxValue, ErrorMessage = Messages.SelectValidBrand)]
        public long BrandId { get; set; }
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
