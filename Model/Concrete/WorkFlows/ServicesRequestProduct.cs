using Core.Enums;
using Microsoft.EntityFrameworkCore;
using Model.Abstractions;
using System.ComponentModel.DataAnnotations;

namespace Model.Concrete.WorkFlows
{
    // Ara tablo: N-N (ServicesRequest <-> Product)
    public class ServicesRequestProduct : BaseEntity
    {
        [Key]
        public long Id { get; set; }
        public required string RequestNo { get; set; }
        public required long ProductId { get; set; }
        public Product Product { get; set; } = default!;
        public Customer? Customer { get; set; } = default!;
        public required long CustomerId { get; set; }
        public int Quantity { get; set; }

        // -- Mevcut dinamik hesaplar (akış içinde kullanılıyor) --
        public decimal TotalPrice => Quantity * (Product?.Price ?? 0m);

        public decimal GetEffectivePrice()
        {
            if (Customer?.CustomerGroup?.GroupProductPrices
                .FirstOrDefault(gp => gp.ProductId == ProductId) is { } groupPrice)
                return groupPrice.Price;

            if (Customer?.CustomerProductPrices
                .FirstOrDefault(cp => cp.ProductId == ProductId) is { } customerPrice)
                return customerPrice.Price;

            return Product?.Price ?? 0m;
        }

        public decimal GetTotalEffectivePrice()
        {
            if (Customer?.CustomerGroup?.GroupProductPrices
                .FirstOrDefault(gp => gp.ProductId == ProductId) is { } groupPrice)
                return Quantity * groupPrice.Price;

            if (Customer?.CustomerProductPrices
                .FirstOrDefault(cp => cp.ProductId == ProductId) is { } customerPrice)
                return Quantity * customerPrice.Price;

            return Quantity * (Product?.Price ?? 0m);
        }

        // ---------- YENİ: “o anki” fiyatı sabitleyen alanlar ----------
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
