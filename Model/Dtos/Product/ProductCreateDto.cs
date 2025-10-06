using Core.Utilities.Constants;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Model.Dtos.Product
{
    [PriceNeedsCurrency(nameof(Price), nameof(PriceCurrency), nameof(CurrencyTypeId),
        ErrorMessage = Messages.CurrencyRequiredIfPriceEntered)]
    [DateOrder(nameof(InstallationDate), nameof(ConnectionDate),
        ErrorMessage = Messages.ConnectionDateBeforeInstallationDate)]
    [ModelNeedsBrand(nameof(ModelId), nameof(BrandId),
        ErrorMessage = Messages.BrandRequiredIfModelSelected)]
    public class ProductCreateDto
    {
        [StringLength(64, ErrorMessage = Messages.ProductCodeMaxLength)]
        [RegexIfNotEmpty(@"^[A-Za-z0-9._-]+$", ErrorMessage = Messages.ProductCodeInvalidChars)]
        public string? ProductCode { get; set; }

        [StringLength(64, ErrorMessage = Messages.OracleCodeMaxLength)]
        [RegexIfNotEmpty(@"^[A-Za-z0-9._-]+$", ErrorMessage = Messages.OracleCodeInvalidChars)]
        public string? OracleProductCode { get; set; }

        [StringLength(64, ErrorMessage = Messages.SystemTypeMaxLength)]
        [NotWhitespaceIfNotEmpty(ErrorMessage = Messages.SystemTypeCannotBeWhitespace)]
        public string? SystemType { get; set; }

        [RangeIfHasValue(1, long.MaxValue, ErrorMessage = Messages.SelectValidBrand)]
        public long? BrandId { get; set; }

        [RangeIfHasValue(1, long.MaxValue, ErrorMessage = Messages.SelectValidModel)]
        public long? ModelId { get; set; }

        [StringLength(500, ErrorMessage = Messages.DescriptionMaxLength)]
        public string? Description { get; set; }

        [RegularExpression(@"^[A-Z]{3}$", ErrorMessage = Messages.CurrencyCodeFormat)]
        public string? PriceCurrency { get; set; }

        [NonNegativeDecimalIfHasValue(ErrorMessage = Messages.PriceCannotBeNegative)]
        public decimal? Price { get; set; }

        [RangeIfHasValue(1, long.MaxValue, ErrorMessage = Messages.SelectValidCurrency)]
        public long? CurrencyTypeId { get; set; }

        public DateTimeOffset? InstallationDate { get; set; }
        public DateTimeOffset? ConnectionDate { get; set; }

        [StringLength(32, ErrorMessage = Messages.CorporateShortCodeMaxLength)]
        [RegexIfNotEmpty(@"^[A-Za-z0-9._-]+$", ErrorMessage = Messages.CorporateShortCodeInvalidChars)]
        public string? CorporateCustomerShortCode { get; set; }

        [StringLength(64, ErrorMessage = Messages.OracleCustomerCodeMaxLength)]
        [RegexIfNotEmpty(@"^[A-Za-z0-9._-]+$", ErrorMessage = Messages.OracleCustomerCodeInvalidChars)]
        public string? OracleCustomerCode { get; set; }

        [RangeIfHasValue(1, long.MaxValue, ErrorMessage = Messages.SelectValidProductType)]
        public long? ProductTypeId { get; set; }
    }

    // ---------- Yardımcı Attribute'lar ----------

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

    /// Nullable long verildiyse min–max aralığında olmalı.
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class RangeIfHasValueAttribute : ValidationAttribute
    {
        public long Min { get; }
        public long Max { get; }
        public RangeIfHasValueAttribute(long min, long max) { Min = min; Max = max; }

        protected override ValidationResult? IsValid(object? value, ValidationContext _)
        {
            if (value is null) return ValidationResult.Success;
            if (value is long lv && lv >= Min && lv <= Max) return ValidationResult.Success;
            return new ValidationResult(ErrorMessage ?? $"{Messages.ValueOutOfRange}");
        }
    }

    /// Nullable decimal verildiyse negatif olamaz.
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class NonNegativeDecimalIfHasValueAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext _)
        {
            if (value is null) return ValidationResult.Success;
            if (value is decimal d && d >= 0) return ValidationResult.Success;
            return new ValidationResult(ErrorMessage ?? Messages.ValueCannotBeNegative);
        }
    }

    /// Price varsa PriceCurrency veya CurrencyTypeId zorunlu.
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class PriceNeedsCurrencyAttribute : ValidationAttribute
    {
        public string PriceProperty { get; }
        public string CurrencyCodeProperty { get; }
        public string CurrencyIdProperty { get; }

        public PriceNeedsCurrencyAttribute(string priceProperty, string currencyCodeProperty, string currencyIdProperty)
        {
            PriceProperty = priceProperty;
            CurrencyCodeProperty = currencyCodeProperty;
            CurrencyIdProperty = currencyIdProperty;
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext ctx)
        {
            if (value is null) return ValidationResult.Success;

            var pProp = ctx.ObjectType.GetProperty(PriceProperty);
            var cProp = ctx.ObjectType.GetProperty(CurrencyCodeProperty);
            var idProp = ctx.ObjectType.GetProperty(CurrencyIdProperty);
            if (pProp is null || cProp is null || idProp is null) return ValidationResult.Success;

            var price = pProp.GetValue(value) as decimal?;
            if (price is null) return ValidationResult.Success;

            var code = cProp.GetValue(value) as string;
            var id = idProp.GetValue(value) as long?;

            var hasCode = !string.IsNullOrWhiteSpace(code);
            var hasId = id.HasValue && id.Value > 0;

            if (hasCode || hasId) return ValidationResult.Success;

            return new ValidationResult(ErrorMessage ?? Messages.CurrencyInfoMissing);
        }
    }

    /// ModelId varsa BrandId zorunlu.
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ModelNeedsBrandAttribute : ValidationAttribute
    {
        public string ModelIdProperty { get; }
        public string BrandIdProperty { get; }
        public ModelNeedsBrandAttribute(string modelIdProperty, string brandIdProperty)
        {
            ModelIdProperty = modelIdProperty; BrandIdProperty = brandIdProperty;
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext ctx)
        {
            if (value is null) return ValidationResult.Success;

            var mProp = ctx.ObjectType.GetProperty(ModelIdProperty);
            var bProp = ctx.ObjectType.GetProperty(BrandIdProperty);
            if (mProp is null || bProp is null) return ValidationResult.Success;

            var modelId = mProp.GetValue(value) as long?;
            if (modelId is null) return ValidationResult.Success;

            var brandId = bProp.GetValue(value) as long?;
            if (brandId.HasValue && brandId.Value > 0) return ValidationResult.Success;

            return new ValidationResult(ErrorMessage ?? Messages.BrandRequiredIfModelSelected);
        }
    }

    /// InstallationDate ≤ ConnectionDate (ikisi de varsa)
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class DateOrderAttribute : ValidationAttribute
    {
        public string FromProperty { get; }
        public string ToProperty { get; }
        public DateOrderAttribute(string fromProperty, string toProperty)
        { FromProperty = fromProperty; ToProperty = toProperty; }

        protected override ValidationResult? IsValid(object? value, ValidationContext ctx)
        {
            if (value is null) return ValidationResult.Success;

            var f = ctx.ObjectType.GetProperty(FromProperty)?.GetValue(value) as DateTimeOffset?;
            var t = ctx.ObjectType.GetProperty(ToProperty)?.GetValue(value) as DateTimeOffset?;
            if (f.HasValue && t.HasValue && t.Value < f.Value)
                return new ValidationResult(ErrorMessage ?? Messages.InvalidDateOrder);
            return ValidationResult.Success;
        }
    }
}
