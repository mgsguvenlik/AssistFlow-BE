using Model.Dtos.Region;

namespace Model.Dtos.City
{
    public class CityGetDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Code { get; set; }
        public List<RegionGetDto> Regions { get; set; } = new();
    }
}
