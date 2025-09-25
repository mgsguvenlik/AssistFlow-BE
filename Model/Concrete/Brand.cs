using Model.Abstractions;

namespace Model.Concrete
{
    // Brand (Marka)
    public class Brand : SoftDeleteEntity
    {
        public long Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Desc { get; set; }

        public ICollection<Model> Models { get; set; } = new List<Model>();
        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
