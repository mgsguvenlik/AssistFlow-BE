using Microsoft.AspNetCore.Http;

namespace Model.Dtos.Tenant
{
    public class TenantUpdateDto
    {
        public long  Id {  get; set; }
        public string? Name { get; set; } 
        public string? Code { get; set; } 
        public IFormFile? LogoFile { get; set; }
        public string? Logo { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
