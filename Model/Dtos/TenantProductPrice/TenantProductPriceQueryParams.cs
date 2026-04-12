namespace Model.Dtos.TenantProductPrice
{
    public class TenantProductPriceQueryParams
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string? Search { get; set; }
        public string? Sort { get; set; }
        public bool Desc { get; set; }

        // 🔍 Filtreler
        public long? ProductId { get; set; }
        public long? TenantId { get; set; }
    }
}