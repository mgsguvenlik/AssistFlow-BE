
using Core.Utilities.Constants;
using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.User
{
    public class UserUpdateDto
    {
        public long Id { get; set; }

        [Required(ErrorMessage = Messages.TechnicianCodeRequired)]
        [MaxLength(50)]
        [RegularExpression(@"^\S+$", ErrorMessage = Messages.TechnicianCodeNoSpaces)] 
        public string TechnicianCode { get; set; } = string.Empty;

        public string? TechnicianCompany { get; set; }
        public string? TechnicianAddress { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
        public string TechnicianName { get; set; } = string.Empty;
        public string? TechnicianPhone { get; set; }
        public string? TechnicianEmail { get; set; }
        public bool IsActive { get; set; }
        // Şifre değiştirme opsiyonel
        public string? NewPassword { get; set; }
        public long? TenantId { get; set; }
        public List<long>? RoleIds { get; set; }
    }
}
