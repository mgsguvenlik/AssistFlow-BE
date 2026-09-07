namespace Model.Dtos.WorkFlowDtos.EkbDtos.EkbServicesRequestProduct
{
    public class EkbServicesRequestProductGetDto
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
        public bool? IsServiceFeeProduct { get; set; }        // Hizmet bedeli ürünü mü?
        public decimal? ServiceFeePercentage { get; set; }  // Uygulanacak yüzde oranı
    }
}
