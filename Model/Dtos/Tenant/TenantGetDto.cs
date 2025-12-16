namespace Model.Dtos.Tenant
{
    public class TenantGetDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public bool IsActive { get; set; }
        public string? CreatedByUserName { get; set; }
        public DateTimeOffset CreatedDate { get; set; }
        public long? CreatedUser { get; set; }
        public DateTimeOffset? UpdatedDate { get; set; }
        public long? UpdatedUser { get; set; }
    }
}
