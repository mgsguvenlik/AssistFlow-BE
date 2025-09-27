namespace Model.Dtos.Auth
{
    public class CurrentUserDto
    {
        public bool IsAuthenticated { get; set; }
        public long Id { get; set; }
        public string Name { get; set; } = "";
        public string? Email { get; set; }
        public List<string> Roles { get; set; } = new();
    }
}
