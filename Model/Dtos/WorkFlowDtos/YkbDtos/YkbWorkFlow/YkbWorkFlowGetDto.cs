using Core.Enums;
using Model.Dtos.User;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbWorkFlowStep;

namespace Model.Dtos.WorkFlowDtos.YkbDtos.YkbWorkFlow
{
    public class YkbWorkFlowGetDto
    {
        public long Id { get; set; }
        public string RequestTitle { get; set; } = string.Empty;
        public string RequestNo { get; set; } = string.Empty;

        public long? CurrentStepId { get; set; }
        public string? CurrentStepCode { get; set; }
        public WorkFlowPriority Priority { get; set; }
        public bool? IsAgreement { get; set; }
        public bool IsLocationValid { get; set; }
        public string? CustomerApproverName { get; set; }
        public WorkFlowStatus WorkFlowStatus { get; set; }
        public long? ApproverTechnicianId { get; set; }
        public string? ApproverTechnicianName { get; set; }
        public YkbWorkFlowStepGetDto? CurrentStep { get; set; }  // ilişki

        public DateTimeOffset CreatedDate { get; set; }
        public DateTimeOffset? UpdatedDate { get; set; }
        public long CreatedUser { get; set; }
        public long? UpdatedUser { get; set; }
        public bool IsDeleted { get; set; }
        public string? CustomerCode { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerAddress { get; set; }
        public UserGetDto? ApproverTechnician { get; set; }
    }

}
