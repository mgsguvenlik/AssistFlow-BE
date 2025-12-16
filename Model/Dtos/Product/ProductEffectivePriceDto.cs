namespace Model.Dtos.Product
{
    public class ProductEffectivePriceDto
    {
        public long ProductId { get; set; }
        public string? ProductCode { get; set; }
        public string? Description { get; set; }
        public decimal? BasePrice { get; set; }
        public string? BaseCurrency { get; set; }
        public decimal ProductPrice { get; set; }
        public decimal EffectivePrice { get; set; }
        public string? EffectiveCurrency { get; set; }
    }
}
