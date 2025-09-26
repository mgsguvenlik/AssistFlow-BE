namespace Model.Dtos.Role
{
    public class RoleGetDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Code { get; set; }
    }
}
