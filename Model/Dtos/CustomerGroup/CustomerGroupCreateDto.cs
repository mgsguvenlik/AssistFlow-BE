using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.CustomerGroup
{
    public class CustomerGroupCreateDto
    {
        [Required(ErrorMessage = "Grup adı zorunludur.")]
        [StringLength(120, MinimumLength = 2, ErrorMessage = "Grup adı 2-120 karakter olmalıdır.")]
        [NotWhitespace(ErrorMessage = "Grup adı yalnızca boşluklardan oluşamaz.")]
        public string GroupName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Kod zorunludur.")]
        [StringLength(32, MinimumLength = 2, ErrorMessage = "Kod 2-32 karakter olmalıdır.")]
        [RegularExpression(@"^[A-Z0-9._-]+$", ErrorMessage = "Kod yalnızca A-Z, 0-9, '.', '_' ve '-' içerebilir.")]
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
            return new ValidationResult(ErrorMessage ?? "Değer yalnızca boşluk olamaz.");
        }
    }
}
