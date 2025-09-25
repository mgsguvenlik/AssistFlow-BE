using Model.Abstractions;
using System.ComponentModel.DataAnnotations;

namespace Model.Concrete
{
    public class CustomerType : BaseEntity
    {
        /// <summary>
        /// Birincil anahtar (PK). Otomatik artan kimlik.
        /// </summary>
        [Key]
        public long Id { get; set; }

        /// <summary>
        /// Müşteri tipinin adı (ör. Bireysel, Kurumsal, Bayi vb.).
        /// </summary>
        [Required]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Müşteri tipine özel kısa kod (örn. IND, CORP, DIST).
        /// </summary>
        [Required]
        public string Code { get; set; } = string.Empty;
    }
}
