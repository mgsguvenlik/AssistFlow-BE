namespace Model.Dtos.TenantProductPrice
{
    public class TenantProductPriceCreateDto
    {
        public long TenantId { get; set; }
        public long ProductId { get; set; }
        public decimal Price { get; set; }
        public string? CurrencyCode { get; set; }
        public string? Name { get; set; }
    }
}