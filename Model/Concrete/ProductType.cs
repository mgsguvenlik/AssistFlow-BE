using Model.Abstractions;
using System.ComponentModel.DataAnnotations;

namespace Model.Concrete
{
    public class ProductType : BaseEntity
    {
        [Key]
        public long Id { get; set; }
        public string Type { get; set; } = null!;
        public string? Code { get; set; }

        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
