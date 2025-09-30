using Model.Abstractions;
using System.ComponentModel.DataAnnotations;

namespace Model.Concrete
{
    public class User:AuditableWithUserEntity
    {
        /// <summary>Birincil anahtar (PK).</summary>
        [Key]
        public long Id { get; set; }

        /// <summary>Teknisyenin benzersiz kodu (iç sistem/CRM kodu).</summary>
        [Required, MaxLength(50)]
        public string TechnicianCode { get; set; } = string.Empty;

        /// <summary>Bağlı olduğu firma/unvan.</summary>
        [MaxLength(200)]
        public string? TechnicianCompany { get; set; }

        /// <summary>Açık adres.</summary>
        public string? TechnicianAddress { get; set; }

        /// <summary>İl.</summary>
        [MaxLength(100)]
        public string? City { get; set; }

        /// <summary>İlçe.</summary>
        [MaxLength(100)]
        public string? District { get; set; }

        /// <summary>Ad Soyad.</summary>
        [Required, MaxLength(150)]
        public string TechnicianName { get; set; } = string.Empty;

        /// <summary>Telefon numarası.</summary>
        [MaxLength(30), Phone]
        public string? TechnicianPhone { get; set; }

        /// <summary>E-posta adresi.</summary>
        [MaxLength(254), EmailAddress]
        public string? TechnicianEmail { get; set; }

        /// <summary>
        /// Şifre hash'i. **Düz metin şifre saklamayın.**
        /// Örn. PBKDF2/BCrypt/Argon2 gibi algoritmalarla hash'lenmiş değer.
        /// </summary>
        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>
        ///     
        /// </summary>
        public bool IsActive { get; set; } = true;

        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    }
}
