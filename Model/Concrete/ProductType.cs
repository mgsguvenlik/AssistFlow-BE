using Model.Abstractions;

namespace Model.Concrete
{
    public class ProductType : SoftDeleteEntity
    {
        public long Id { get; set; }
        public string Type { get; set; } = null!;
        public string? Code { get; set; }

        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
