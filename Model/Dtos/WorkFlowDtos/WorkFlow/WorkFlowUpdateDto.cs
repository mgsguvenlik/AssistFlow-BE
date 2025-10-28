using Core.Enums;

namespace Model.Dtos.WorkFlowDtos.WorkFlow
{
    public class WorkFlowUpdateDto
    {
        public long Id { get; set; }
        public string? RequestTitle { get; set; }
        public string? RequestNo { get; set; }
        public long? CurrentStepId { get; set; }
        public WorkFlowPriority? Priority { get; set; }
        public bool IsLocationValid { get; set; } = true;
        public WorkFlowStatus WorkFlowStatus { get; set; }
        public bool? IsAgreement { get; set; }
        public long? ApproverTechnicianId { get; set; }
    }
}
