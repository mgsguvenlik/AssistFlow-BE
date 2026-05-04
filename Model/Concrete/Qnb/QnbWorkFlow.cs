using Core.Enums;
using Model.Abstractions;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model.Concrete.Qnb
{
    [Table("QnbWorkFlow", Schema = "qnb")]
    public class QnbWorkFlow : AuditableWithUserEntity
    {
        [Key]
        public long Id { get; set; }

        [Required, MaxLength(250)]
        public string RequestTitle { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string RequestNo { get; set; } = string.Empty;

        [ForeignKey(nameof(CurrentStep))]
        public long? CurrentStepId { get; set; }
        public QnbWorkFlowStep? CurrentStep { get; set; }

        public WorkFlowPriority Priority { get; set; } = WorkFlowPriority.Normal;

        public bool? IsAgreement { get; set; }
        public bool IsLocationValid { get; set; } = true;
        public string? CustomerApproverName { get; set; }

        public WorkFlowStatus WorkFlowStatus { get; set; } = WorkFlowStatus.Pending;

        [ForeignKey(nameof(ApproverTechnician))]
        public long? ApproverTechnicianId { get; set; }
        public User? ApproverTechnician { get; set; }
    }
}