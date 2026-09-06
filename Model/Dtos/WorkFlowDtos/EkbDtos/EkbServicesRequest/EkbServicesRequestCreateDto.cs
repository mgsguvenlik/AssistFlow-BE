using Core.Enums;
using Model.Dtos.WorkFlowDtos.ServicesRequestProduct;
using Model.Dtos.WorkFlowDtos.EkbDtos.EkbServicesRequestProduct;

namespace Model.Dtos.WorkFlowDtos.EkbDtos.EkbServicesRequest
{
    public class EkbServicesRequestCreateDto
    {
        public string RequestNo { get; set; } = string.Empty;
        public string? OracleNo { get; set; }

        public DateTimeOffset ServicesDate { get; set; }
        public DateTimeOffset? PlannedCompletionDate { get; set; }

        public ServicesCostStatus ServicesCostStatus { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }

        public bool IsMailSended { get; set; }
        public bool IsLocationValid { get; set; }

        public bool IsProductRequirement { get; set; }
        public long? WorkFlowStepId { get; set; }

        public long CustomerId { get; set; }
        public long ServiceTypeId { get; set; }

        public long? CustomerApproverId { get; set; }

        public long? ApproverTechnicianId { get; set; }
        public string? CustomerApproverName { get; set; }

        public WorkFlowPriority Priority { get; set; } = WorkFlowPriority.Normal;
        public ServicesRequestStatus ServicesRequestStatus { get; set; }

        public List<EkbServicesRequestProductCreateDto>? Products { get; set; }
    }

}
