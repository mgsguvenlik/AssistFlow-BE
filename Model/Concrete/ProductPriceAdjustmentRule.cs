using Core.Enums;
using Model.Abstractions;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model.Concrete
{
    /// <summary>
    /// Tenant ve ürün bazında uygulanabilecek fiyat düzenleme kuralı.
    /// </summary>
    public class ProductPriceAdjustmentRule : AuditableWithUserEntity
    {
        [Key]
        public long Id { get; set; }

        /// <summary>
        /// Kuralın geçerli olduğu tenant.
        /// </summary>
        public long TenantId { get; set; }

        public Tenant Tenant { get; set; } = null!;

        /// <summary>
        /// Kuralın uygulanabileceği ürün.
        /// </summary>
        public long ProductId { get; set; }

        public Product Product { get; set; } = null!;

        /// <summary>
        /// Sistemsel kural kodu.
        /// Örnek: SPECIAL_PRODUCT_SURCHARGE
        /// </summary>
        [Required]
        [MaxLength(64)]
        public string Code { get; set; } = null!;

        /// <summary>
        /// Ekranda gösterilecek kural adı.
        /// </summary>
        [Required]
        [MaxLength(250)]
        public string Name { get; set; } = null!;

        /// <summary>
        /// Yüzde veya sabit tutar.
        /// </summary>
        public PriceAdjustmentType AdjustmentType { get; set; }

        /// <summary>
        /// Ekleme veya çıkarma.
        /// </summary>
        public PriceAdjustmentDirection Direction { get; set; }

        /// <summary>
        /// Satır toplamı veya ürün adedi bazlı hesaplama.
        /// </summary>
        public PriceAdjustmentCalculationBasis CalculationBasis { get; set; }
            = PriceAdjustmentCalculationBasis.LineTotal;

        /// <summary>
        /// Varsayılan yüzde veya tutar.
        /// </summary>
        [Column(TypeName = "numeric(18,4)")]
        public decimal? DefaultValue { get; set; }

        /// <summary>
        /// Kullanıcının ekrandan oran/tutar değiştirmesine izin verilir mi?
        /// </summary>
        public bool IsValueEditable { get; set; } = true;

        /// <summary>
        /// Kullanıcının girebileceği minimum değer.
        /// </summary>
        [Column(TypeName = "numeric(18,4)")]
        public decimal? MinimumValue { get; set; }

        /// <summary>
        /// Kullanıcının girebileceği maksimum değer.
        /// </summary>
        [Column(TypeName = "numeric(18,4)")]
        public decimal? MaximumValue { get; set; }

        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Yönetim ekranında gösterilebilecek açıklama.
        /// </summary>
        [MaxLength(500)]
        public string? Description { get; set; }
    }
}