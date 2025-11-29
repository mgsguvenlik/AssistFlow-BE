using Model.Dtos.Role;

namespace Model.Dtos.Auth
{
    public class CurrentUserDto
    {
        public bool IsAuthenticated { get; set; }
        public long Id { get; set; }
        public string Name { get; set; } = "";
        public string? Email { get; set; }
        public string TechnicianCode { get; set; } = string.Empty;
        public string? TechnicianCompany { get; set; }
        public string? TechnicianAddress { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
        public string TechnicianName { get; set; } = string.Empty;
        public string? TechnicianPhone { get; set; }
        public string? TechnicianEmail { get; set; }

        // 🔹 Tenant alanları
        public long? TenantId { get; set; }
        public string? TenantCode { get; set; }
        public string? TenantName { get; set; }
        public List<RoleGetDto> Roles { get; set; } = new();
    }
}
