using Core.Utilities.Constants;
using Model.Dtos.ProgressApprover;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Model.Dtos.Customer
{
    [RequireAny(nameof(Phone1), nameof(Email1),
        ErrorMessage = Messages.PrimaryContactRequired)]
    [RequireAny(nameof(SubscriberCompany), nameof(ContactName1),
        ErrorMessage = Messages.CustomerNameRequired)]
    public class CustomerCreateDto
    {
        // Kodlar: harf, rakam, ., _, - (opsiyonel)
        [StringLength(64, ErrorMessage = Messages.SubscriberCodeMaxLength)]
        [RegexIfNotEmpty(@"^[A-Za-z0-9._-]+$", ErrorMessage = Messages.SubscriberCodeInvalidChars)]
        public string? SubscriberCode { get; set; }

        [StringLength(200, ErrorMessage = Messages.SubscriberCompanyMaxLength)]
        [NotWhitespaceIfNotEmpty(ErrorMessage = Messages.SubscriberCompanyCannotBeWhitespace)]
        public string? SubscriberCompany { get; set; }

        [StringLength(500, ErrorMessage = Messages.AddressMaxLength)]
        public string? SubscriberAddress { get; set; }

        [StringLength(100, ErrorMessage = Messages.CityMaxLength)]
        public string? City { get; set; }

        [StringLength(100, ErrorMessage = Messages.CityMaxLength)]
        public string? District { get; set; }

        [StringLength(64, ErrorMessage = Messages.LocationCodeMaxLength)]
        [RegexIfNotEmpty(@"^[A-Za-z0-9._-]+$", ErrorMessage = Messages.LocationCodeInvalidChars)]
        public string? LocationCode { get; set; }


        [StringLength(120, ErrorMessage = Messages.FirstPersonNameMaxLength)]
        [NotWhitespaceIfNotEmpty(ErrorMessage = Messages.FirstPersonNameCannotBeWhitespace)]
        public string? ContactName1 { get; set; }

        // Telefon: +905551112233 veya 05551112233 gibi (7-15 rakam, isteğe bağlı +)
        [RegexIfNotEmpty(@"^\+?[0-9]{7,15}$", ErrorMessage = Messages.PhoneNumberFormat)]
        public string? Phone1 { get; set; }

        [EmailAddress(ErrorMessage = Messages.EnterValidEmail)]
        [StringLength(200, ErrorMessage = Messages.EmailMaxLength)]
        public string? Email1 { get; set; }

        public string? ContactName2 { get; set; }

        public string? Phone2 { get; set; }

        public string? Email2 { get; set; }

        [StringLength(32, ErrorMessage = Messages.CustomerShortCodeMaxLength)]
        [RegexIfNotEmpty(@"^[A-Za-z0-9._-]+$", ErrorMessage = Messages.CustomerShortCodeInvalidChars)]
        public string? CustomerShortCode { get; set; }

        [StringLength(64, ErrorMessage = Messages.CorporateLocationIdMaxLength)]
        [RegexIfNotEmpty(@"^[A-Za-z0-9._-]+$", ErrorMessage = Messages.CorporateLocationIdInvalidChars)]
        public string? CorporateLocationId { get; set; }

        // Nullable olduğu için boş geçilebilir; değer girilirse 1 ve üzeri olmalı
        [Range(1, long.MaxValue, ErrorMessage = Messages.CustomerTypeInvalid)]
        public long? CustomerTypeId { get; set; }
        public string? Longitude { get; set; }
        public string? Latitude { get; set; }
        public long? CustomerGroupId { get; set; }
        public DateTimeOffset? InstallationDate { get; set; }
        public int? WarrantyYears { get; set; }
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
            return new ValidationResult(ErrorMessage ?? $"{Messages.AtLeastOneFieldRequired} {string.Join(", ", PropertyNames)}");
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
            return new ValidationResult(ErrorMessage ?? Messages.InvalidFormat);
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
            return new ValidationResult(ErrorMessage ?? Messages.ValueCannotBeWhitespace);
        }
    }
}
