using Model.Abstractions;
using System.ComponentModel.DataAnnotations;

namespace Model.Concrete
{
    public class Tenant : AuditableWithUserEntity
    {
        [Key]
        public long Id { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [MaxLength(260)]
        public string? LogoUrl { get; set; }
         
        public bool IsActive { get; set; } = true;

        // Navi. props
        public ICollection<Customer> Customers { get; set; } = new List<Customer>();
        public ICollection<User> Users { get; set; } = new List<User>();
    }
}
