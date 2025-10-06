namespace Model.Dtos.User
{
    public class AssignUserRolesDto
    {
        public long UserId { get; set; }
        public List<long> RoleIds { get; set; } = new();
    }
}
