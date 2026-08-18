namespace Model.Dtos.Crm.PurchaseRequestItem
{
    public class PurchaseRequestItemGetDto
    {
        public long Id { get; set; }

        public long PurchaseRequestId { get; set; }

        public int LineNo { get; set; }


        // Product
        public long? ProductId { get; set; }

        public string? ProductCode { get; set; }

        public string? ProductName { get; set; }

        public decimal Quantity { get; set; }

        public string? BrandName { get; set; }

        public string? ModelName { get; set; }

        public string? Description { get; set; }


        // Alternate
        public long? AlternateProductId { get; set; }

        public string? AlternateProductCode { get; set; }

        public string? AlternateProductName { get; set; }


        // Supplier
        public string? SupplierName { get; set; }


        // Pricing
        public decimal? SupplierListPrice { get; set; }

        public decimal? SupplierDiscountRate { get; set; }

        public decimal? SupplierNetPrice { get; set; }

        public long? CurrencyTypeId { get; set; }

        public string? CurrencyCode { get; set; }

        public string? CurrencyName { get; set; }


        // Procurement
        public string? StockStatus { get; set; }

        public string? Maturity { get; set; }

        public string? CompanyCode { get; set; }

        public bool RequiresWarehouseControl { get; set; }

        public bool? IsConfirmed { get; set; }


        // Audit
        public DateTimeOffset CreatedDate { get; set; }

        public DateTimeOffset? UpdatedDate { get; set; }
    }
}