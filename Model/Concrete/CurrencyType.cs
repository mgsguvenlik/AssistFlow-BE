using Model.Abstractions;
using System.ComponentModel.DataAnnotations;

namespace Model.Concrete
{
    public class CurrencyType : BaseEntity
    {
        [Key]
        public long Id { get; set; }
        public string Code { get; set; } = null!; // Örn: USD, EUR
        public string? Name { get; set; }         // Örn: Amerikan Doları

        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
