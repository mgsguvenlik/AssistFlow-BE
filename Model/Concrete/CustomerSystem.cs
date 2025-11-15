using Model.Abstractions;
using System.ComponentModel.DataAnnotations;

namespace Model.Concrete
{
    public class CustomerSystem : AuditableWithUserEntity
    {
        [Key]
        public long Id { get; set; }

        /// <summary>
        /// Sistem adı (örn. SAP, CRM, Portal).
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = null!;

        /// <summary>
        /// Sistem kodu (örn. SAP, CRM001, PORTAL01).
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Code { get; set; } = null!;

        // Many-to-many navigation
        public ICollection<Customer> Customers { get; set; } = new List<Customer>();

   
    }
}
