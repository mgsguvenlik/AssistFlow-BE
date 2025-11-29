using Model.Dtos.Role;

namespace Model.Dtos.User
{
    public class UserGetDto
    {
        public long Id { get; set; }
        public long? TenantId { get; set; }
        public string? TenantCode { get; set; }
        public string? TenantName { get; set; }
        public string TechnicianCode { get; set; } = string.Empty;
        public string? TechnicianCompany { get; set; }
        public string? TechnicianAddress { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
        public string TechnicianName { get; set; } = string.Empty;
        public string? TechnicianPhone { get; set; }
        public string? TechnicianEmail { get; set; }

        public bool IsActive { get; set; }
        public List<RoleGetDto> Roles { get; set; } = new();

    }
}
