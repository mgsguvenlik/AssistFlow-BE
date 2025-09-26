namespace Model.Dtos.User
{
    public class UserCreateDto
    {
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public List<Guid> Roles { get; set; }
    }
}
