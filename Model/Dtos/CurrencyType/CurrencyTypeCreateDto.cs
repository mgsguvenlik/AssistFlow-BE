using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.CurrencyType
{
    public class CurrencyTypeCreateDto
    {
        [Required(ErrorMessage = "Para birimi kodu zorunludur.")]
        [RegularExpression(@"^[A-Z]{3}$",
            ErrorMessage = "Para birimi kodu ISO 4217 formatında 3 büyük harf olmalıdır (örn. TRY, USD, EUR).")]
        public string Code { get; set; } = string.Empty;

        [StringLength(120, ErrorMessage = "Ad en fazla 120 karakter olabilir.")]
        [NotWhitespace(ErrorMessage = "Ad yalnızca boşluklardan oluşamaz.")]
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
            return new ValidationResult(ErrorMessage ?? "Değer yalnızca boşluk olamaz.");
        }
    }
}
