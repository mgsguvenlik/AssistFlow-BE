namespace Model.Dtos.User
{
    public class UserCreateDto
    {
        public string TechnicianCode { get; set; } = string.Empty;
        public string? TechnicianCompany { get; set; }
        public string? TechnicianAddress { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
        public string TechnicianName { get; set; } = string.Empty;
        public string? TechnicianPhone { get; set; }
        public string? TechnicianEmail { get; set; }

        // Düz şifre asla taşımayalım; servis katmanı hashlesin:
        public string Password { get; set; } = string.Empty;

        // Rolleri id listesiyle bağlayabilirsin:
        public List<long>? RoleIds { get; set; }
    }
}
