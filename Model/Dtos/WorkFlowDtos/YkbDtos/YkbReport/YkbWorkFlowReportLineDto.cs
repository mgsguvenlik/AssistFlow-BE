namespace Model.Dtos.WorkFlowDtos.YkbDtos.YkbReport
{
    public sealed class YkbWorkFlowReportLineDto
    {
        public string? RequestNo { get; set; }
        public string? City { get; set; }
        public string? CustomerName { get; set; }
        public string? ProductCode { get; set; }
        public string? LocationCode { get; set; }
        public string? ProductOracleCode { get; set; }
        public string? ProductDefinition { get; set; }

        public DateTimeOffset? ServiceDate { get; set; }
        public string? ServiceOracleNo { get; set; }
        public string? WorkOrder { get; set; }

        public int? Quantity { get; set; }

        public decimal? LineUnitPriceTL { get; set; }
        public decimal? LineTotalTL { get; set; }
        public decimal? LineUnitPriceUSD { get; set; }
        public decimal? LineTotalUSD { get; set; }

        public string? GLCode { get; set; }
        public string? MGSDescription { get; set; }

        public string? ContractNo { get; set; }
        public string? CostType { get; set; }
        public string? Description { get; set; }
        public decimal DiscountPercent { get; set; }

        public DateTimeOffset? InstallationDate { get; set; }
    }
}
