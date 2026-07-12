using Core.Enums;

namespace Model.Dtos.ProductPriceAdjustmentRule
{
    public class ProductPriceAdjustmentRuleGetDto
    {
        public long Id { get; set; }

        public long TenantId { get; set; }
        public string? TenantName { get; set; }
        public string? TenantCode { get; set; }

        public long ProductId { get; set; }
        public string? ProductCode { get; set; }
        public string? ProductName { get; set; }

        public string Code { get; set; } = null!;
        public string Name { get; set; } = null!;

        public PriceAdjustmentType AdjustmentType { get; set; }
        public PriceAdjustmentDirection Direction { get; set; }
        public PriceAdjustmentCalculationBasis CalculationBasis { get; set; }

        public decimal? DefaultValue { get; set; }
        public bool IsValueEditable { get; set; }
        public decimal? MinimumValue { get; set; }
        public decimal? MaximumValue { get; set; }

        public bool IsActive { get; set; }
        public string? Description { get; set; }

        // DateTime yerine DateTimeOffset
        public DateTimeOffset CreatedDate { get; set; }
        public long? CreatedUser { get; set; }

        public DateTimeOffset? UpdatedDate { get; set; }
        public long? UpdatedUser { get; set; }
    }
}