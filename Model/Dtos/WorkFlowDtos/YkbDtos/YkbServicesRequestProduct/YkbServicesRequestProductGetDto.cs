using Model.Dtos.ProductPriceAdjustmentRule;

namespace Model.Dtos.WorkFlowDtos.YkbDtos.YkbServicesRequestProduct
{
    public class YkbServicesRequestProductGetDto
    {
        public long Id { get; set; }
        public string RequestNo { get; set; } = string.Empty;
        public long ProductId { get; set; }
        public string? ProductName { get; set; }
        public long CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public int Quantity { get; set; }
        public decimal TotalPrice { get; set; }
        public bool IsPriceCaptured { get; set; }
        public decimal? CapturedUnitPrice { get; set; }
        public string? CapturedCurrency { get; set; }
        public decimal? CapturedTotal { get; set; }
        public decimal ProductPrice { get; set; }
        public decimal EffectivePrice { get; set; }
        public string? ProductCode { get; set; }
        public string? PriceCurrency { get; set; }

        /// <summary>
        /// Bu ürün YKB tenantı için özel ürün olarak tanımlanmış mı?
        /// </summary>
        public bool IsSpecialPriceProduct { get; set; }

        /// <summary>
        /// Bu talepte özel fiyat daha önce uygulanmış mı?
        /// </summary>
        public bool IsPriceAdjustmentApplied { get; set; }

        /// <summary>
        /// Uygulanan yüzde veya tutar.
        /// </summary>
        public decimal? AppliedPriceAdjustmentValue { get; set; }

        /// <summary>
        /// Kullanıcı bu ekranda özel fiyat uygulayabilir mi?
        /// </summary>
        public bool CanApplyPriceAdjustment { get; set; }

        public ProductPriceAdjustmentRuleGetDto? PriceAdjustmentRule { get; set; }
    }
}
