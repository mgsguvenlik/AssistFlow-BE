using Microsoft.EntityFrameworkCore;
using Model.Abstractions;
using Model.Concrete.WorkFlows;
using Model.Concrete.Ykb;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model.Concrete
{
    /// <summary>
    /// İş emri türü tanımı.
    /// Örn: Bakım, Arıza, Kurulum, Keşif
    /// </summary>
    [Index(nameof(Code), IsUnique = true)]
    public class WorkOrderType : BaseEntity
    {
        [Key]
        public long Id { get; set; }

        [Required]
        [MaxLength(120)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(64)]
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// Bu iş emri türünün bağlı olduğu servis talepleri.
        /// </summary>
        public ICollection<ServicesRequestWorkOrderType> ServicesRequestWorkOrderTypes { get; set; }
            = new List<ServicesRequestWorkOrderType>();

        /// <summary>
        /// Bu iş emri türünün bağlı olduğu YKB servis talepleri.
        /// </summary>
        public ICollection<YkbServicesRequestWorkOrderType> YkbServicesRequestWorkOrderTypes { get; set; }
            = new List<YkbServicesRequestWorkOrderType>();
    }
}