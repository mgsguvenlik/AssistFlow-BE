using Core.Enums;
using Core.Enums.Ekb;
using Model.Dtos.Customer;
using Model.Dtos.WorkFlowDtos.ServicesRequestProduct;
using Model.Dtos.WorkFlowDtos.WorkFlowReviewLog;
using Model.Dtos.WorkFlowDtos.EkbDtos.EkbReviewLog;
using Model.Dtos.WorkFlowDtos.EkbDtos.EkbServicesRequestProduct;
using Model.Dtos.WorkOrderType;

namespace Model.Dtos.WorkFlowDtos.EkbDtos.EkbCustomerForm
{
    public class EkbCustomerFormGetDto
    {
        public long Id { get; set; }
        public string RequestNo { get; set; } = string.Empty;
        public string? EkbServiceTrackNo { get; set; }
        public DateTime ServicesDate { get; set; }
        public DateTime? PlannedCompletionDate { get; set; }

        public long CustomerId { get; set; }
        public string? CustomerName { get; set; }

        public long? CustomerApproverId { get; set; }
        public string? CustomerApproverName { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public EkbCustomerFormStatus Status { get; set; }
        public WorkFlowPriority Priority { get; set; }

        public DateTimeOffset CreatedDate { get; set; }
        public DateTimeOffset? UpdatedDate { get; set; }
        public long CreatedUser { get; set; }
        public long? UpdatedUser { get; set; }

        public bool IsDeleted { get; set; }

        public long? ServiceTypeId { get; set; }
        public ServicesCostStatus ServicesCostStatus { get; set; }

        public List<EkbServicesRequestProductGetDto> ServicesRequestProducts { get; set; } = new();
        public List<EkbWorkFlowReviewLogDto> ReviewLogs { get; set; } = new();
        public CustomerGetDto? Customer { get; set; }
        public List<long>? WorkOrderTypeIds { get; set; }
        public List<WorkOrderTypeGetDto> WorkOrderTypes { get; set; } = new();
    }
}
