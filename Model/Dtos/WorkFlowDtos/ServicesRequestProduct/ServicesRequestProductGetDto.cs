namespace Model.Dtos.WorkFlowDtos.ServicesRequestProduct
{
    public class ServicesRequestProductGetDto
    {
        public long Id { get; set; }
        public required string  RequestNo { get; set; }
        public long ProductId { get; set; }
        public decimal ProductPrice { get; set; }
        public decimal EffectivePrice { get; set; }
        public string? ProductName { get; set; }
        public string? ProductCode { get; set; }
        public int Quantity { get; set; }
        public string? PriceCurrency { get; set; }
        public decimal TotalPrice => Quantity * EffectivePrice;
    }
}
