using Model.Abstractions;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model.Concrete
{
    /// <summary>
    /// Müşteri ile sistem arasındaki ilişkiyi ve bakım anlaşması durumunu tutar.
    /// Örn:
    ///  Customer: Burak Türk
    ///  System: Alarm
    ///  HasMaintenanceContract = true/false
    /// </summary>
    public class CustomerSystemAssignment : AuditableWithUserEntity
    {
        [Key]
        public long Id { get; set; }

        [ForeignKey(nameof(Customer))]
        public long CustomerId { get; set; }
        public Customer Customer { get; set; } = null!;

        [ForeignKey(nameof(CustomerSystem))]
        public long CustomerSystemId { get; set; }
        public CustomerSystem CustomerSystem { get; set; } = null!;

        /// <summary>
        /// Bu müşteri–sistem çifti için bakım anlaşması var mı?
        /// </summary>
        public bool HasMaintenanceContract { get; set; }
    }
}
