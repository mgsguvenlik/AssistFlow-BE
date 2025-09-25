using Model.Abstractions;
using System.ComponentModel.DataAnnotations;

namespace Model.Concrete
{
    /// <summary>
    /// Hakediş yetkilisi
    /// </summary>
    public class ProgressApprover : AuditableWithUserEntity
    {
        /// <summary>
        /// Birincil anahtar (PK). Otomatik artan kimlik.
        /// </summary>
        [Key]
        public long Id { get; set; }

        /// <summary>
        /// Yetkilinin adı soyadı (örn. "Ahmet Yılmaz").
        /// </summary>
        [Required, MaxLength(150)]
        public string FullName { get; set; } = string.Empty;

        /// <summary>
        /// Yetkilinin e-posta adresi.
        /// </summary>
        [Required, MaxLength(254), EmailAddress]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Bağlı olduğu müşteri kaydının kimliği (Customer.Id).
        /// </summary>
        [Required]
        public long CustomerId { get; set; }

        /// <summary>
        /// İlişkisel navigasyon: Yetkilinin bağlı olduğu müşteri.
        /// </summary>
        public Customer? Customer { get; set; }
    }
}
