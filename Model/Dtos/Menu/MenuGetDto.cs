namespace Model.Dtos.Menu
{
    public class MenuGetDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
    }
}
