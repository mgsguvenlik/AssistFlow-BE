using Model.Dtos.Product;

namespace Model.Dtos.PriceGroup
{
    public class PriceGroupGetDto
    {
        public long Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public decimal Price { get; set; }

        public long ProductId { get; set; }
        public ProductGetDto? Product { get; set; }   // include ile gelir
    }
}
