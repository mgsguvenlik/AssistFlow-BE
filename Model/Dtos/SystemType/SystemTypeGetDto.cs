namespace Model.Dtos.SystemType
{
    public class SystemTypeGetDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Code { get; set; }
    }
}
