using Core.Enums;
using Core.Enums.Ekb;
using Model.Abstractions;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model.Concrete.Ekb
{
    [Table("EkbCustomerForm", Schema = "ekb")]
    public class EkbCustomerForm : AuditableWithUserEntity
    {
        public long Id { get; set; }


        [Required, MaxLength(100)]
        public string RequestNo { get; set; } = string.Empty;

        /// <summary>
        /// EKB Servis Takip Numarası (Oracle yerine).
        /// </summary>
        public string? EkbServiceTrackNo { get; set; }
        public DateTime ServicesDate { get; set; }          // zorunlu
        public DateTime? PlannedCompletionDate { get; set; } // opsiyonel - Planlanan Tamamlanma Tarihi

        /// <summary>
        /// İlgili müşteri / lokasyon / abone (sadece EKB lokasyonları listelenecek).
        /// </summary>
        public long CustomerId { get; set; }
        public Customer? Customer { get; set; }


        [ForeignKey(nameof(CustomerApproverId))]
        public ProgressApprover? CustomerApprover { get; set; }  // müşteri tarafı onaylayan (opsiyonel)
        public long? CustomerApproverId { get; set; }  // müşteri tarafı onaylayan (opsiyonel)


        /// <summary>
        /// Müşterinin ilk açıklaması / talep detayı.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Form durumu: Draft, Submitted, Cancelled vb.
        /// </summary>
        public EkbCustomerFormStatus Status { get; set; }

        public WorkFlowPriority Priority { get; set; } = WorkFlowPriority.Normal;

    }
}
