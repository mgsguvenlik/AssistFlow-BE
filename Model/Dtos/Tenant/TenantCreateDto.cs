using Microsoft.AspNetCore.Http;

namespace Model.Dtos.Tenant
{
    public class TenantCreateDto
    {
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public IFormFile? LogoFile { get; set; }
        public string? LogoUrl { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
