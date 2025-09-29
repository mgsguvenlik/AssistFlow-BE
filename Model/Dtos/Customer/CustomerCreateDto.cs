using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Model.Dtos.Customer
{
    [RequireAny(nameof(Phone1), nameof(Email1),
        ErrorMessage = "Birincil iletişim için en az Telefon veya E-posta girilmelidir.")]
    [RequireAny(nameof(SubscriberCompany), nameof(ContactName1),
        ErrorMessage = "Müşteri adı için 'Abone Firma' veya '1. Kişi Adı' alanlarından en az biri dolu olmalıdır.")]
    public class CustomerCreateDto
    {
        // Kodlar: harf, rakam, ., _, - (opsiyonel)
        [StringLength(64, ErrorMessage = "Abone Kodu en fazla 64 karakter olabilir.")]
        [RegexIfNotEmpty(@"^[A-Za-z0-9._-]+$", ErrorMessage = "Abone Kodu yalnızca harf, rakam, '.', '_' ve '-' içerebilir.")]
        public string? SubscriberCode { get; set; }

        [StringLength(200, ErrorMessage = "Abone Firma en fazla 200 karakter olabilir.")]
        [NotWhitespaceIfNotEmpty(ErrorMessage = "Abone Firma yalnızca boşluklardan oluşamaz.")]
        public string? SubscriberCompany { get; set; }

        [StringLength(120, ErrorMessage = "Müşteri Ana Grup Adı en fazla 120 karakter olabilir.")]
        public string? CustomerMainGroupName { get; set; }

        [StringLength(500, ErrorMessage = "Adres en fazla 500 karakter olabilir.")]
        public string? SubscriberAddress { get; set; }

        [StringLength(100, ErrorMessage = "İl en fazla 100 karakter olabilir.")]
        public string? City { get; set; }

        [StringLength(64, ErrorMessage = "Lokasyon Kodu en fazla 64 karakter olabilir.")]
        [RegexIfNotEmpty(@"^[A-Za-z0-9._-]+$", ErrorMessage = "Lokasyon Kodu yalnızca harf, rakam, '.', '_' ve '-' içerebilir.")]
        public string? LocationCode { get; set; }

        [StringLength(64, ErrorMessage = "Oracle Kodu en fazla 64 karakter olabilir.")]
        [RegexIfNotEmpty(@"^[A-Za-z0-9._-]+$", ErrorMessage = "Oracle Kodu yalnızca harf, rakam, '.', '_' ve '-' içerebilir.")]
        public string? OracleCode { get; set; }

        [StringLength(120, ErrorMessage = "1. Kişi adı en fazla 120 karakter olabilir.")]
        [NotWhitespaceIfNotEmpty(ErrorMessage = "1. Kişi adı yalnızca boşluklardan oluşamaz.")]
        public string? ContactName1 { get; set; }

        // Telefon: +905551112233 veya 05551112233 gibi (7-15 rakam, isteğe bağlı +)
        [RegexIfNotEmpty(@"^\+?[0-9]{7,15}$", ErrorMessage = "Telefon 7-15 haneli olmalı ve sadece rakam (isteğe bağlı +) içermelidir.")]
        public string? Phone1 { get; set; }

        [EmailAddress(ErrorMessage = "Geçerli bir e-posta girin.")]
        [StringLength(200, ErrorMessage = "E-posta en fazla 200 karakter olabilir.")]
        public string? Email1 { get; set; }

        [StringLength(120, ErrorMessage = "2. Kişi adı en fazla 120 karakter olabilir.")]
        [NotWhitespaceIfNotEmpty(ErrorMessage = "2. Kişi adı yalnızca boşluklardan oluşamaz.")]
        public string? ContactName2 { get; set; }

        [RegexIfNotEmpty(@"^\+?[0-9]{7,15}$", ErrorMessage = "Telefon 7-15 haneli olmalı ve sadece rakam (isteğe bağlı +) içermelidir.")]
        public string? Phone2 { get; set; }

        [EmailAddress(ErrorMessage = "Geçerli bir e-posta girin.")]
        [StringLength(200, ErrorMessage = "E-posta en fazla 200 karakter olabilir.")]
        public string? Email2 { get; set; }

        [StringLength(32, ErrorMessage = "Müşteri Kısa Kodu en fazla 32 karakter olabilir.")]
        [RegexIfNotEmpty(@"^[A-Za-z0-9._-]+$", ErrorMessage = "Müşteri Kısa Kodu yalnızca harf, rakam, '.', '_' ve '-' içerebilir.")]
        public string? CustomerShortCode { get; set; }

        [StringLength(64, ErrorMessage = "Kurumsal Lokasyon ID en fazla 64 karakter olabilir.")]
        [RegexIfNotEmpty(@"^[A-Za-z0-9._-]+$", ErrorMessage = "Kurumsal Lokasyon ID yalnızca harf, rakam, '.', '_' ve '-' içerebilir.")]
        public string? CorporateLocationId { get; set; }

        // Nullable olduğu için boş geçilebilir; değer girilirse 1 ve üzeri olmalı
        [Range(1, long.MaxValue, ErrorMessage = "Müşteri Tipi geçersiz.")]
        public long? CustomerTypeId { get; set; }
    }

    /// ---- Yardımcı Attribute'lar ----

    /// Sınıf düzeyinde: belirtilen özelliklerden en az biri dolu olmalı.
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class RequireAnyAttribute : ValidationAttribute
    {
        public string[] PropertyNames { get; }
        public RequireAnyAttribute(params string[] propertyNames) => PropertyNames = propertyNames;

        protected override ValidationResult? IsValid(object? value, ValidationContext context)
        {
            if (value is null) return ValidationResult.Success;
            foreach (var name in PropertyNames ?? Array.Empty<string>())
            {
                var prop = context.ObjectType.GetProperty(name);
                if (prop == null) continue;
                var val = prop.GetValue(value);
                if (val is string s && !string.IsNullOrWhiteSpace(s)) return ValidationResult.Success;
                if (val is not string && val is not null) return ValidationResult.Success;
            }
            return new ValidationResult(ErrorMessage ?? $"Alanlardan en az biri dolu olmalıdır: {string.Join(", ", PropertyNames)}");
        }
    }

    /// Boş değilse regex’e uymalı (boş ise valid).
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class RegexIfNotEmptyAttribute : ValidationAttribute
    {
        private readonly Regex _regex;
        public RegexIfNotEmptyAttribute(string pattern) =>
            _regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);

        protected override ValidationResult? IsValid(object? value, ValidationContext _)
        {
            if (value is null) return ValidationResult.Success;
            if (value is string s && s.Length == 0) return ValidationResult.Success;
            if (value is string s2 && _regex.IsMatch(s2)) return ValidationResult.Success;
            return new ValidationResult(ErrorMessage ?? "Geçersiz biçim.");
        }
    }

    /// Boş değilse yalnızca boşluklardan oluşamaz.
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class NotWhitespaceIfNotEmptyAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext _)
        {
            if (value is null) return ValidationResult.Success;
            if (value is string s && s.Length == 0) return ValidationResult.Success;
            if (value is string s2 && !string.IsNullOrWhiteSpace(s2)) return ValidationResult.Success;
            return new ValidationResult(ErrorMessage ?? "Değer yalnızca boşluk olamaz.");
        }
    }
}
