using Model.Abstractions;
using System.ComponentModel.DataAnnotations;

namespace Model.Concrete
{
    public class UserRole : BaseEntity
    {
        /// <summary>Birincil anahtar (PK).</summary>
        [Key]
        public long Id { get; set; }

        /// <summary>Kullanıcı FK'sı (User.Id).</summary>
        [Required]
        public long UserId { get; set; }

        /// <summary>Rol FK'sı (Role.Id).</summary>
        [Required]
        public long RoleId { get; set; }

        // Navigations (isteğe bağlı ama önerilir)
        public User? User { get; set; }
        public Role? Role { get; set; }
    }
}
