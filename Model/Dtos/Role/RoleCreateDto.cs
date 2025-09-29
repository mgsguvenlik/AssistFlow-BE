using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.Role
{
    public class RoleCreateDto
    {
        [Required(ErrorMessage = "Rol adı zorunludur.")]
        [StringLength(64, MinimumLength = 2, ErrorMessage = "Rol adı 2-64 karakter olmalıdır.")]
        [NotWhitespace(ErrorMessage = "Rol adı yalnızca boşluklardan oluşamaz.")]
        public string Name { get; set; } = string.Empty;

        [StringLength(32, MinimumLength = 2, ErrorMessage = "Kod 2-32 karakter olmalıdır.")]
        [RegularExpression(@"^[A-Z0-9._-]+$", ErrorMessage = "Kod yalnızca A-Z, 0-9, '.', '_' ve '-' içerebilir.")]
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
            return new ValidationResult(ErrorMessage ?? "Değer yalnızca boşluk olamaz.");
        }
    }
}
