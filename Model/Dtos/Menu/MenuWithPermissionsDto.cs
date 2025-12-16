namespace Model.Dtos.Menu
{
    public class MenuWithPermissionsDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public bool CanView { get; set; }
        public bool CanEdit { get; set; }
    }
}
