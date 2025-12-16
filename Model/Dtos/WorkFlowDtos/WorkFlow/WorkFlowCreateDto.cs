using Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.WorkFlowDtos.WorkFlow
{
    public class WorkFlowCreateDto
    {
        public string RequestTitle { get; set; } = null!;

        [Required, MaxLength(100)]
        public string RequestNo { get; set; } = null!;

        public long CurrentStepId { get; set; } // WorkFlowStep FK
        public WorkFlowPriority Priority { get; set; } = WorkFlowPriority.Normal;

        public bool IsLocationValid { get; set; } = true;

        public WorkFlowStatus WorkFlowStatus { get; set; }

        public bool? IsAgreement { get; set; }
        public long? ApproverTechnicianId { get; set; }
    }
}
