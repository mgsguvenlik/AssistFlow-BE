using Core.Enums;
using Model.Abstractions;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model.Concrete.Ykb
{
    [Table("YkbWorkFlow", Schema = "ykb")]
    public class YkbWorkFlow : AuditableWithUserEntity // yoksa : class
    {
        [Key]
        public long Id { get; set; }

        [Required, MaxLength(250)]
        public string RequestTitle { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string RequestNo { get; set; } = string.Empty;


        // WorkFlow'un şu anda hangi adımda olduğunu takip etmek için (Opsiyonel ama faydalı)
        [ForeignKey(nameof(CurrentStep))]
        public long? CurrentStepId { get; set; }
        public YkbWorkFlowStep? CurrentStep { get; set; }

        // Öncelik
        public WorkFlowPriority Priority { get; set; } = WorkFlowPriority.Normal;

        // Bayraklar
        public bool? IsAgreement { get; set; }
        public bool IsLocationValid { get; set; } = true;
        public string? CustomerApproverName { get; set; }

        // Mutabakat durumu
        public WorkFlowStatus WorkFlowStatus { get; set; } = WorkFlowStatus.Pending;

        /// <summary>Onaylayan teknisyen (opsiyonel)</summary>
        [ForeignKey(nameof(ApproverTechnician))]
        public long? ApproverTechnicianId { get; set; }
        public User? ApproverTechnician { get; set; }


    }
}
