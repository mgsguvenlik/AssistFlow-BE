using Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.ProductPriceAdjustmentRule
{
    public class ProductPriceAdjustmentRuleCreateDto : IValidatableObject
    {
        [Range(1, long.MaxValue, ErrorMessage = "Geçerli bir tenant seçilmelidir.")]
        public long TenantId { get; set; }

        [Range(1, long.MaxValue, ErrorMessage = "Geçerli bir ürün seçilmelidir.")]
        public long ProductId { get; set; }

        [Required(ErrorMessage = "Kural kodu zorunludur.")]
        [StringLength(64, ErrorMessage = "Kural kodu en fazla 64 karakter olabilir.")]
        [RegularExpression(
            @"^[A-Za-z0-9._-]+$",
            ErrorMessage = "Kural kodu yalnızca harf, rakam, nokta, alt çizgi ve tire içerebilir.")]
        public string Code { get; set; } = null!;

        [Required(ErrorMessage = "Kural adı zorunludur.")]
        [StringLength(250, ErrorMessage = "Kural adı en fazla 250 karakter olabilir.")]
        public string Name { get; set; } = null!;

        [EnumDataType(
            typeof(PriceAdjustmentType),
            ErrorMessage = "Geçerli bir fiyat düzenleme tipi seçilmelidir.")]
        public PriceAdjustmentType AdjustmentType { get; set; }

        [EnumDataType(
            typeof(PriceAdjustmentDirection),
            ErrorMessage = "Geçerli bir fiyat düzenleme yönü seçilmelidir.")]
        public PriceAdjustmentDirection Direction { get; set; }

        [EnumDataType(
            typeof(PriceAdjustmentCalculationBasis),
            ErrorMessage = "Geçerli bir hesaplama şekli seçilmelidir.")]
        public PriceAdjustmentCalculationBasis CalculationBasis { get; set; }
            = PriceAdjustmentCalculationBasis.LineTotal;

        public decimal? DefaultValue { get; set; }

        public bool IsValueEditable { get; set; } = true;

        public decimal? MinimumValue { get; set; }

        public decimal? MaximumValue { get; set; }

        public bool IsActive { get; set; } = true;

        [StringLength(500, ErrorMessage = "Açıklama en fazla 500 karakter olabilir.")]
        public string? Description { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!Enum.IsDefined(typeof(PriceAdjustmentType), AdjustmentType))
            {
                yield return new ValidationResult(
                    "Geçerli bir fiyat düzenleme tipi seçilmelidir.",
                    new[] { nameof(AdjustmentType) });
            }

            if (!Enum.IsDefined(typeof(PriceAdjustmentDirection), Direction))
            {
                yield return new ValidationResult(
                    "Geçerli bir fiyat düzenleme yönü seçilmelidir.",
                    new[] { nameof(Direction) });
            }

            if (!Enum.IsDefined(typeof(PriceAdjustmentCalculationBasis), CalculationBasis))
            {
                yield return new ValidationResult(
                    "Geçerli bir hesaplama şekli seçilmelidir.",
                    new[] { nameof(CalculationBasis) });
            }

            if (DefaultValue.HasValue && DefaultValue.Value <= 0)
            {
                yield return new ValidationResult(
                    "Varsayılan değer sıfırdan büyük olmalıdır.",
                    new[] { nameof(DefaultValue) });
            }

            if (MinimumValue.HasValue && MinimumValue.Value < 0)
            {
                yield return new ValidationResult(
                    "Minimum değer negatif olamaz.",
                    new[] { nameof(MinimumValue) });
            }

            if (MaximumValue.HasValue && MaximumValue.Value <= 0)
            {
                yield return new ValidationResult(
                    "Maksimum değer sıfırdan büyük olmalıdır.",
                    new[] { nameof(MaximumValue) });
            }

            if (MinimumValue.HasValue &&
                MaximumValue.HasValue &&
                MinimumValue.Value > MaximumValue.Value)
            {
                yield return new ValidationResult(
                    "Minimum değer, maksimum değerden büyük olamaz.",
                    new[] { nameof(MinimumValue), nameof(MaximumValue) });
            }

            if (DefaultValue.HasValue &&
                MinimumValue.HasValue &&
                DefaultValue.Value < MinimumValue.Value)
            {
                yield return new ValidationResult(
                    "Varsayılan değer, minimum değerden küçük olamaz.",
                    new[] { nameof(DefaultValue), nameof(MinimumValue) });
            }

            if (DefaultValue.HasValue &&
                MaximumValue.HasValue &&
                DefaultValue.Value > MaximumValue.Value)
            {
                yield return new ValidationResult(
                    "Varsayılan değer, maksimum değerden büyük olamaz.",
                    new[] { nameof(DefaultValue), nameof(MaximumValue) });
            }

            if (!IsValueEditable && !DefaultValue.HasValue)
            {
                yield return new ValidationResult(
                    "Kullanıcı değeri değiştiremiyorsa varsayılan değer zorunludur.",
                    new[] { nameof(DefaultValue), nameof(IsValueEditable) });
            }

            // Yüzde hesabı doğrudan ürün satır toplamı üzerinden uygulanacak.
            if (AdjustmentType == PriceAdjustmentType.Percentage &&
                CalculationBasis != PriceAdjustmentCalculationBasis.LineTotal)
            {
                yield return new ValidationResult(
                    "Yüzde bazlı fiyat düzenlemelerinde hesaplama şekli satır toplamı olmalıdır.",
                    new[] { nameof(CalculationBasis) });
            }
        }
    }
}