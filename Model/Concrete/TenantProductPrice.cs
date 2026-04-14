using Model.Abstractions;
using System.ComponentModel.DataAnnotations;

namespace Model.Concrete
{
    /// <summary>
    /// Tenant bazlý ürün fiyatý (üçüncü öncelik - Customer ve Group'tan sonra).
    /// (Tenant, Product) ikilisi benzersizdir.
    /// </summary>
    public class TenantProductPrice : AuditableWithUserEntity
    {
        [Key]
        public long Id { get; set; }

        [Required]
        public long TenantId { get; set; }
        public Tenant Tenant { get; set; } = default!;

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