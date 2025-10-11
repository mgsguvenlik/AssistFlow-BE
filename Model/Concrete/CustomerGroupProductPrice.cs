using Model.Abstractions;
using System.ComponentModel.DataAnnotations;

namespace Model.Concrete
{
    /// <summary>
    /// Grup bazlı ürün fiyatı (ikincil öncelik).
    /// (CustomerGroup, Product) ikilisi benzersizdir.
    /// </summary>
    public class CustomerGroupProductPrice : AuditableWithUserEntity
    {
        [Key]
        public long Id { get; set; }

        [Required]
        public long CustomerGroupId { get; set; }
        public CustomerGroup CustomerGroup { get; set; } = default!;

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
