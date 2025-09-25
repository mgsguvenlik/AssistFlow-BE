
namespace Model.Dtos.User
{
    public class UserSelfUpdateDto
    {
        public Guid Id { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
    }
}
