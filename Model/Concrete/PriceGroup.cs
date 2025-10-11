using Model.Abstractions;
using System.ComponentModel.DataAnnotations;

namespace Model.Concrete
{
    public class PriceGroup : AuditableWithUserEntity
    {
        [Key]
        public long Id { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }

        [Required]
        public decimal Price { get; set; }

        public long ProductId { get; set; }
        public Product Product { get; set; }

        public ICollection<Customer> Customers { get; set; } = new List<Customer>();

    }
}
