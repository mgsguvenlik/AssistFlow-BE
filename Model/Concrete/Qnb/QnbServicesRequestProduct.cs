using Core.Enums;
using Microsoft.EntityFrameworkCore;
using Model.Abstractions;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model.Concrete.Qnb
{
    [Table("QnbServicesRequestProduct", Schema = "qnb")]
    public class QnbServicesRequestProduct : BaseEntity
    {
        [Key]
        public long Id { get; set; }
        public required string RequestNo { get; set; }
        public required long ProductId { get; set; }
        public Product Product { get; set; } = default!;
        public Customer? Customer { get; set; } = default!;
        public long? CustomerId { get; set; }
        public int Quantity { get; set; }

        public decimal TotalPrice => Quantity * (Product?.Price ?? 0m);

        public decimal GetEffectivePrice()
        {

            return GetEffectivePriceWithCurrency().Price;
           
        }

        public (decimal Price, string? CurrencyCode) GetEffectivePriceWithCurrency()
        {
            // 1. Grup fiyatý
            var groupPrice = Customer?.CustomerGroup?.GroupProductPrices.FirstOrDefault(x => x.ProductId == ProductId);
            if (groupPrice is not null)
            {
                return (groupPrice.Price, groupPrice.CurrencyCode);
            }

            // 2. Müþteri özel fiyatý
            var customerPrice = Customer?.CustomerProductPrices.FirstOrDefault(x => x.ProductId == ProductId);

            if (customerPrice is not null)
            {
                return (customerPrice.Price, customerPrice.CurrencyCode);
            }

            // 3. Tenant fiyatý
            var tenantPrice = Customer?.Tenant?.TenantProductPrices.FirstOrDefault(x => x.ProductId == ProductId);

            if (tenantPrice is not null)
            {
                return (tenantPrice.Price, tenantPrice.CurrencyCode);
            }

            // 4. Ürün genel fiyatý
            return (Product?.Price ?? 0m, Product?.PriceCurrency);
        }
        public decimal GetTotalEffectivePrice()
        {
            if (Customer?.CustomerGroup?.GroupProductPrices
                .FirstOrDefault(gp => gp.ProductId == ProductId) is { } groupPrice)
                return Quantity * groupPrice.Price;

            if (Customer?.CustomerProductPrices
                .FirstOrDefault(cp => cp.ProductId == ProductId) is { } customerPrice)
                return Quantity * customerPrice.Price;

            if (Customer?.Tenant?.TenantProductPrices
                .FirstOrDefault(tp => tp.ProductId == ProductId) is { } tenantPrice)
                return Quantity * tenantPrice.Price;

            return Quantity * (Product?.Price ?? 0m);
        }

        // O anki fiyatý sabitleyen alanlar
        public bool IsPriceCaptured { get; set; }

        [Precision(18, 2)]
        public decimal? CapturedUnitPrice { get; set; }

        [MaxLength(3)]
        public string? CapturedCurrency { get; set; }

        [Precision(18, 2)]
        public decimal? CapturedTotal { get; set; }

        public CapturedPriceSource? CapturedSource { get; set; }
        public DateTime? CapturedAt { get; set; }
    }
}