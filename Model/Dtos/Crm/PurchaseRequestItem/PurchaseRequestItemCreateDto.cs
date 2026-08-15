using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.Crm.PurchaseRequestItem
{
    public class PurchaseRequestItemCreateDto
    {
        [Required]
        public long PurchaseRequestId { get; set; }

        public int LineNo { get; set; }

        public long? ProductId { get; set; }

        public string? ProductName { get; set; }

        [Required]
        public decimal Quantity { get; set; }

        public string? BrandName { get; set; }

        public string? ModelName { get; set; }

        public string? Description { get; set; }

        public long? AlternateProductId { get; set; }

        public string? AlternateProductName { get; set; }

        public string? SupplierName { get; set; }

        public decimal? SupplierListPrice { get; set; }

        public decimal? SupplierDiscountRate { get; set; }

        public decimal? SupplierNetPrice { get; set; }

        public long? CurrencyTypeId { get; set; }

        public string? StockStatus { get; set; }

        public string? Maturity { get; set; }

        public string? CompanyCode { get; set; }

        public bool RequiresWarehouseControl { get; set; }

        public bool? IsConfirmed { get; set; }
    }
}