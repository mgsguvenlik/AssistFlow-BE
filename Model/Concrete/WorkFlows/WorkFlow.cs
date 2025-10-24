using Core.Enums;
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
        public bool IsLocationValid { get; set; } = true;
        public string? CustomerApproverName { get; set; }

        // Mutabakat durumu
        public WorkFlowReconciliationStatus ReconciliationStatus { get; set; } = WorkFlowReconciliationStatus.Pending;

        /// <summary>Onaylayan teknisyen (opsiyonel)</summary>
        [ForeignKey(nameof(ApproverTechnician))]
        public long? ApproverTechnicianId { get; set; }
        public User? ApproverTechnician { get; set; }
    } 
}
