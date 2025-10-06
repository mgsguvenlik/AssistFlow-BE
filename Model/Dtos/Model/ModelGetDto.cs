using Model.Dtos.Brand;

namespace Model.Dtos.Model
{
    public class ModelGetDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Desc { get; set; }
        public BrandGetDto? Brand { get; set; }
    }
}
