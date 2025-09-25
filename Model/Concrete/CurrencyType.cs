using Model.Abstractions;

namespace Model.Concrete
{
    public class CurrencyType : BaseEntity
    {
        public long Id { get; set; }
        public string Code { get; set; } = null!; // Örn: USD, EUR
        public string? Name { get; set; }         // Örn: Amerikan Doları

        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
