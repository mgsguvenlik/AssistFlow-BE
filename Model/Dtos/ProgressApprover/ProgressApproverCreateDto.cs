using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.ProgressApprover
{
    public class ProgressApproverCreateDto
    {
        [Required(ErrorMessage = "Ad Soyad zorunludur.")]
        [StringLength(120, MinimumLength = 2, ErrorMessage = "Ad Soyad 2-120 karakter olmalıdır.")]
        [NotWhitespace(ErrorMessage = "Ad Soyad yalnızca boşluklardan oluşamaz.")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "E-posta zorunludur.")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta giriniz.")]
        [StringLength(200, ErrorMessage = "E-posta en fazla 200 karakter olabilir.")]
        public string Email { get; set; } = string.Empty;

        [Range(1, long.MaxValue, ErrorMessage = "Geçerli bir müşteri seçiniz.")]
        public long CustomerId { get; set; }
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
