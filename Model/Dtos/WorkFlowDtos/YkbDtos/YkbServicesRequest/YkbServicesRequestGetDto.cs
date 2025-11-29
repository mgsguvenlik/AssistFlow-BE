using Core.Enums;
using Model.Dtos.Customer;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbReviewLog;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbServicesRequestProduct;

namespace Model.Dtos.WorkFlowDtos.YkbDtos.YkbServicesRequest
{
    public class YkbServicesRequestGetDto
    {
        public long Id { get; set; }
        public string RequestNo { get; set; } = string.Empty;
        public string? OracleNo { get; set; }
        public DateTimeOffset ServicesDate { get; set; }
        public DateTimeOffset? PlannedCompletionDate { get; set; }
        public ServicesCostStatus ServicesCostStatus { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public bool IsProductRequirement { get; set; }
        public long? WorkFlowStepId { get; set; }
        public string? WorkFlowStepCode { get; set; }
        public long? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public long? ServiceTypeId { get; set; }
        public string? ServiceTypeName { get; set; }
        public long? CustomerApproverId { get; set; }
        public string? CustomerApproverName { get; set; }
        public WorkFlowPriority Priority { get; set; }
        public ServicesRequestStatus ServicesRequestStatus { get; set; }
        public bool IsMailSended { get; set; }
        public string ServicesCostStatusText => ServicesCostStatus.ToString();
        public bool IsLocationValid { get; set; }
        public string? WorkFlowStepName { get; set; }
        public DateTimeOffset CreatedDate { get; set; }
        public DateTimeOffset? UpdatedDate { get; set; }
        public long CreatedUser { get; set; }
        public long? UpdatedUser { get; set; }
        public bool IsDeleted { get; set; }
        public long? ApproverTechnicianId { get; set; }
        public List<YkbServicesRequestProductGetDto> ServicesRequestProducts { get; set; } = new();
        public List<YkbWorkFlowReviewLogDto> ReviewLogs { get; set; } = new();
        public CustomerGetDto? Customer { get; set; }
    }
}
