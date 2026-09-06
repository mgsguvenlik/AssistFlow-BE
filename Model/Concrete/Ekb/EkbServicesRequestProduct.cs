using Core.Enums;
using Microsoft.EntityFrameworkCore;
using Model.Abstractions;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model.Concrete.Ekb
{
    [Table("EkbServicesRequestProduct", Schema = "ekb")]
    public class EkbServicesRequestProduct : BaseEntity
    {
        [Key]
        public long Id { get; set; }
        public required string RequestNo { get; set; }
        public required long ProductId { get; set; }
        public Product Product { get; set; } = default!;
        public Customer? Customer { get; set; } = default!;
        public long? CustomerId { get; set; }
        public int Quantity { get; set; }

        // -- Mevcut dinamik hesaplar (akış içinde kullanılıyor) --
        public decimal TotalPrice => Quantity * (Product?.Price ?? 0m);

        public decimal GetEffectivePrice()
        {
            return GetEffectivePriceWithCurrency().Price;

       
        }

        public decimal GetTotalEffectivePrice()
        {
            // 1️⃣ Grup fiyatı
            if (Customer?.CustomerGroup?.GroupProductPrices
                .FirstOrDefault(gp => gp.ProductId == ProductId) is { } groupPrice)
                return Quantity * groupPrice.Price;

            // 2️⃣ Müşteri özel fiyatı
            if (Customer?.CustomerProductPrices
                .FirstOrDefault(cp => cp.ProductId == ProductId) is { } customerPrice)
                return Quantity * customerPrice.Price;

            // 3️⃣ Tenant fiyatı 🆕
            if (Customer?.Tenant?.TenantProductPrices
                .FirstOrDefault(tp => tp.ProductId == ProductId) is { } tenantPrice)
                return Quantity * tenantPrice.Price;

            // 4️⃣ Ürün genel fiyatı
            return Quantity * (Product?.Price ?? 0m);
        }
        public (decimal Price, string? CurrencyCode) GetEffectivePriceWithCurrency()
        {
            // 1. Grup fiyatı
            var groupPrice = Customer?.CustomerGroup?.GroupProductPrices.FirstOrDefault(x => x.ProductId == ProductId);
            if (groupPrice is not null)
            {
                return (groupPrice.Price, groupPrice.CurrencyCode);
            }

            // 2. Müşteri özel fiyatı
            var customerPrice = Customer?.CustomerProductPrices.FirstOrDefault(x => x.ProductId == ProductId);

            if (customerPrice is not null)
            {
                return (customerPrice.Price, customerPrice.CurrencyCode);
            }

            // 3. Tenant fiyatı
            var tenantPrice = Customer?.Tenant?.TenantProductPrices.FirstOrDefault(x => x.ProductId == ProductId);

            if (tenantPrice is not null)
            {
                return (tenantPrice.Price, tenantPrice.CurrencyCode);
            }

            // 4. Ürün genel fiyatı
            return (Product?.Price ?? 0m, Product?.PriceCurrency);
        }

        // ---------- YENİ: "o anki" fiyatı sabitleyen alanlar ----------
        /// <summary>Fiyat sabitlendi mi? (true ise aşağıdaki captured alanları kullan)</summary>
        public bool IsPriceCaptured { get; set; }

        /// <summary>O an yakalanan birim fiyat</summary>
        [Precision(18, 2)]
        public decimal? CapturedUnitPrice { get; set; }

        /// <summary>O an yakalanan para birimi (örn: TRY, USD)</summary>
        [MaxLength(3)]
        public string? CapturedCurrency { get; set; }

        /// <summary>O an yakalanan toplam (CapturedUnitPrice * Quantity)</summary>
        [Precision(18, 2)]
        public decimal? CapturedTotal { get; set; }

        /// <summary>Fiyatın geldiği kaynak (Standart/Customer/Group)</summary>
        public CapturedPriceSource? CapturedSource { get; set; }

        /// <summary>Fiyatın sabitlendiği zaman</summary>
        public DateTime? CapturedAt { get; set; }
    }
}
