using Model.Abstractions;
using System.ComponentModel.DataAnnotations;

namespace Model.Concrete
{
    /// <summary>
    /// Müşteri bazlı ürün fiyatı (en yüksek öncelik).
    /// (Customer, Product) ikilisi benzersizdir.
    /// </summary>
    public class CustomerProductPrice : AuditableWithUserEntity
    {
        [Key]
        public long Id { get; set; }

        [Required]
        public long CustomerId { get; set; }
        public Customer Customer { get; set; } = default!;

        [Required]
        public long ProductId { get; set; }
        public Product Product { get; set; } = default!;

        [Required]
        public decimal Price { get; set; }

        [MaxLength(10)]
        public string? CurrencyCode { get; set; }

        public string? Name { get; set; }
    }
}
