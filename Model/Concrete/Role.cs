using Model.Abstractions;
using System.ComponentModel.DataAnnotations;

namespace Model.Concrete
{
    public class Role : TimestampedSoftDeleteEntity
    {
        /// <summary>Birincil anahtar (PK).</summary>
        [Key]
        public long Id { get; set; }

        /// <summary>Rol adı (örn. “Operasyon Mühendisi”).</summary>
        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        /// <summary>Rol kısa kodu (opsiyonel, null olabilir).</summary>
        [MaxLength(50)]
        public string? Code { get; set; }
        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    }
}
