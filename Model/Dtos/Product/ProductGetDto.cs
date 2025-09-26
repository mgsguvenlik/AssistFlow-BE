namespace Model.Dtos.Product
{
    public class ProductGetDto
    {
        public long Id { get; set; }
        public string? ProductCode { get; set; }
        public string? OracleProductCode { get; set; }
        public string? SystemType { get; set; }
        public string? Description { get; set; }
        public string? PriceCurrency { get; set; }
        public decimal? Price { get; set; }
        public DateTimeOffset? InstallationDate { get; set; }
        public DateTimeOffset? ConnectionDate { get; set; }
        public string? CorporateCustomerShortCode { get; set; }
        public string? OracleCustomerCode { get; set; }


    }

}
