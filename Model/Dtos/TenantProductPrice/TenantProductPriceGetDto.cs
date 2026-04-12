namespace Model.Dtos.TenantProductPrice
{
    public class TenantProductPriceGetDto
    {
        public long Id { get; set; }
        public long TenantId { get; set; }
        public string? TenantName { get; set; }
        public string? TenantCode { get; set; }
        public long ProductId { get; set; }
        public string? ProductCode { get; set; }
        public string? ProductDescription { get; set; }
        public decimal Price { get; set; }
        public string? CurrencyCode { get; set; }
        public string? Name { get; set; }
    }
}