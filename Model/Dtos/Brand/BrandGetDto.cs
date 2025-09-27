using Model.Dtos.Model;
using Model.Dtos.Product;

namespace Model.Dtos.Brand 
{
    public class BrandGetDto : BrandUpdateDto
    {
        // İlişkileri sadece id + name gibi minimal DTO ile döndürmek daha mantıklı olur.
        public List<ModelGetDto> Models { get; set; } = new();
        public List<ProductGetDto> Products { get; set; } = new();
    }
}
