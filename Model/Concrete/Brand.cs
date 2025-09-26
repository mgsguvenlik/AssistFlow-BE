using Model.Abstractions;
using System.ComponentModel.DataAnnotations;

namespace Model.Concrete
{
    public class Brand : BaseEntity
    {
        [Key]
        public long Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Desc { get; set; }
        public ICollection<Model> Models { get; set; } = new List<Model>();
        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
