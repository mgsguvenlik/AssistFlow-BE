using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.Brand
{
    public class BrandCreateDto
    {
        [Required(ErrorMessage = "Marka adı zorunludur.")]
        [StringLength(120, MinimumLength = 2, ErrorMessage = "Marka adı 2-120 karakter olmalıdır.")]
        [NotWhitespace(ErrorMessage = "Marka adı yalnızca boşluklardan oluşamaz.")]
        public string Name { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Açıklama en fazla 500 karakter olabilir.")]
        public string? Desc { get; set; }
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
