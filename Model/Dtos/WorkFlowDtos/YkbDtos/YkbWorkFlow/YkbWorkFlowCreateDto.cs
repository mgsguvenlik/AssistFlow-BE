using Core.Enums;

namespace Model.Dtos.WorkFlowDtos.YkbDtos.YkbWorkFlow
{
    public class YkbWorkFlowCreateDto
    {
        public string RequestTitle { get; set; } = string.Empty;
        public string RequestNo { get; set; } = string.Empty;

        public long? CurrentStepId { get; set; }
        public WorkFlowPriority Priority { get; set; } = WorkFlowPriority.Normal;

        public bool? IsAgreement { get; set; }
        public bool IsLocationValid { get; set; } = true;
        public string? CustomerApproverName { get; set; }

        public WorkFlowStatus WorkFlowStatus { get; set; } = WorkFlowStatus.Pending;
        public long? ApproverTechnicianId { get; set; }
    }
}
