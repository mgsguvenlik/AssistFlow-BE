using Model.Abstractions;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model.Concrete.WorkFlows
{
    public class WorkFlow : AuditableWithUserEntity // yoksa : class
    {
        [Key]
        public long Id { get; set; }

        [Required, MaxLength(250)]
        public string RequestTitle { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string RequestNo { get; set; } = string.Empty;

        // Status FK
        [ForeignKey(nameof(Status))]
        public long StatuId { get; set; } // diyagramdaki isimle birebir
        public WorkFlowStatus? Status { get; set; }

        // Öncelik
        public WorkFlowPriority Priority { get; set; } = WorkFlowPriority.Normal;

        // Bayraklar
        public bool IsCancelled { get; set; } = false;// iptal edildi mi?
        public bool IsComplated { get; set; } = false; // diyagramdaki yazımı korudum

        // Mutabakat durumu
        public WorkFlowReconciliationStatus ReconciliationStatus { get; set; }
            = WorkFlowReconciliationStatus.Pending;
    }

    // Önerilen enum'lar
    public enum WorkFlowPriority
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Urgent = 3
    }

    public enum WorkFlowReconciliationStatus
    {
        Pending = 0,     // Mutabakat Beklemede
        Completed = 1,   // Mutabakat Tamam
        Rejected = 2,   //Mutabakat Reddedildi

    }
}
