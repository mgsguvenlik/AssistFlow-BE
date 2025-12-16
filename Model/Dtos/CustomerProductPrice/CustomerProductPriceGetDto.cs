namespace Model.Dtos.CustomerProductPrice
{
    public class CustomerProductPriceGetDto
    {
        public long Id { get; set; }
        public long CustomerId { get; set; }
        public long ProductId { get; set; }
        public decimal Price { get; set; }
        public string? CurrencyCode { get; set; }
        public string? Name { get; set; }

        // İsteğe bağlı ekran alanları:
        public string? CustomerName { get; set; }        // map: Customer.SubscriberCompany ?? Customer.ContactName1
        public string? ProductCode { get; set; }         // map: Product.ProductCode
        public string? ProductDescription { get; set; }  // map: Product.Description
    }
}
