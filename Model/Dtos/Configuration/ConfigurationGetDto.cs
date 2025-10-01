namespace Model.Dtos.Configuration
{
    public class ConfigurationGetDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = null!;
        public string Value { get; set; } = null!;
        public string? Description { get; set; }
    }
}
