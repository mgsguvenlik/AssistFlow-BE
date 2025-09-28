namespace Model.Dtos.Region
{
    public class RegionGetDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Code { get; set; }
        public long CityId { get; set; }
    }
}
