using Core.Enums;
using Model.Dtos.User;
using Model.Dtos.WorkFlowDtos.EkbDtos.EkbWorkFlowStep;

namespace Model.Dtos.WorkFlowDtos.EkbDtos.EkbWorkFlow
{
    public class EkbWorkFlowGetDto
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
        public EkbWorkFlowStepGetDto? CurrentStep { get; set; }  // ilişki

        public DateTimeOffset CreatedDate { get; set; }
        public DateTimeOffset? UpdatedDate { get; set; }
        public long CreatedUser { get; set; }
        public long? UpdatedUser { get; set; }
        public string? CreatedUserFullName { get; set; }
        public bool IsDeleted { get; set; }
        public string? CustomerCode { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerAddress { get; set; }
        public UserGetDto? ApproverTechnician { get; set; }
    }

}
