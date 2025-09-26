using Model.Abstractions;
using System.ComponentModel.DataAnnotations;

namespace Model.Concrete
{
    // Model
    public class Model : BaseEntity
    {
        [Key]
        public long Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Desc { get; set; }

        public long BrandId { get; set; }
        public Brand Brand { get; set; } = null!;

        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
