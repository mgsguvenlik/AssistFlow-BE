namespace Model.Dtos.WorkFlowDtos.YkbDtos.YkbReport
{
    public sealed class YkbReportLineRowDto
    {
        public int TotalCount { get; set; }

        public string? RequestNo { get; set; }
        public string? City { get; set; }
        public string? CustomerName { get; set; }
        public string? ProductCode { get; set; }
        public string? LocationCode { get; set; }
        public string? ProductOracleCode { get; set; }
        public string? ProductDefinition { get; set; }

        public DateTimeOffset? ServiceDate { get; set; }      // TechnicalServices.StartTime
        public string? ServiceOracleNo { get; set; }          // ServicesRequests.OracleNo
        public string? WorkOrder { get; set; }                // ServiceType.Name

        public int? Quantity { get; set; }

        public decimal? LineUnitPriceTL { get; set; }
        public decimal? LineTotalTL { get; set; }
        public decimal? LineUnitPriceUSD { get; set; }
        public decimal? LineTotalUSD { get; set; }

        public string? GLCode { get; set; }                   // boş dönüyor (SP)
        public string? MGSDescription { get; set; }           // boş dönüyor (SP)

        public string? Contract_No { get; set; }              // [Contract No]
        public string? CostType { get; set; }
        public string? Description { get; set; }              // boş dönüyor (SP)
        public decimal DiscountPercent { get; set; }
        public DateTimeOffset? InstallationDate { get; set; } // Customers.InstallationDate
    }
}
