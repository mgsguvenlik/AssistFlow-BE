using Model.Abstractions;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model.Concrete.WorkFlows
{
    public class Warehouse : AuditableWithUserEntity
    {
        [Key]
        public long Id { get; set; }

        /// <summary>İlgili akış/talep numarası (örn: SR-2025...)</summary>
        [Required, MaxLength(100)]
        public string RequestNo { get; set; } = string.Empty;

        /// <summary>Teslim tarihi/saat bilgisi</summary>
        [Required]
        public DateTimeOffset DeliveryDate { get; set; }

      
        /// <summary>Açıklama (opsiyonel)</summary>
        public string? Description { get; set; }

        /// <summary>Depodan sevk edildi mi?</summary>
        public bool IsSended { get; set; }
    }
}
