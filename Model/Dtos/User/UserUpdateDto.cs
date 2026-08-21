
using Core.Utilities.Constants;
using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.User
{
    public class UserUpdateDto
    {
        public long Id { get; set; }

        [Required(ErrorMessage = Messages.UserCodeRequired)]
        [MaxLength(50)]
        [RegularExpression(@"^\S+$", ErrorMessage = Messages.UserCodeNoSpaces)]
        public string Code { get; set; } = string.Empty;

        public string? Company { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public bool IsActive { get; set; }
        // Şifre değiştirme opsiyonel
        public string? NewPassword { get; set; }
        public long? TenantId { get; set; }
        public List<long>? RoleIds { get; set; }
    }
}
