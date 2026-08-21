using Model.Dtos.Role;

namespace Model.Dtos.Auth
{
    public class CurrentUserDto
    {
        public bool IsAuthenticated { get; set; }
        public long Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string? Company { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Email { get; set; }

        // 🔹 Tenant alanları
        public long? TenantId { get; set; }
        public string? TenantCode { get; set; }
        public string? TenantName { get; set; }
        public bool? IsTechnicalServiceTestEnabled { get; set; }
        public List<RoleGetDto> Roles { get; set; } = new();
    }
}
