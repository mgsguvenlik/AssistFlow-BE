using Core.Enums;
using Core.Enums.Ykb;
using Model.Dtos.Customer;
using Model.Dtos.WorkFlowDtos.ServicesRequestProduct;
using Model.Dtos.WorkFlowDtos.WorkFlowReviewLog;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbReviewLog;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbServicesRequestProduct;

namespace Model.Dtos.WorkFlowDtos.YkbDtos.YkbCustomerForm
{
    public class YkbCustomerFormGetDto
    {
        public long Id { get; set; }
        public string RequestNo { get; set; } = string.Empty;
        public string? YkbServiceTrackNo { get; set; }
        public DateTime ServicesDate { get; set; }
        public DateTime? PlannedCompletionDate { get; set; }

        public long CustomerId { get; set; }
        public string? CustomerName { get; set; }

        public long? CustomerApproverId { get; set; }
        public string? CustomerApproverName { get; set; }

        public string? Description { get; set; }
        public YkbCustomerFormStatus Status { get; set; }
        public WorkFlowPriority Priority { get; set; }

        public DateTimeOffset CreatedDate { get; set; }
        public DateTimeOffset? UpdatedDate { get; set; }
        public long CreatedUser { get; set; }
        public long? UpdatedUser { get; set; }

        public bool IsDeleted { get; set; }

        public List<YkbServicesRequestProductGetDto> ServicesRequestProducts { get; set; } = new();
        public List<YkbWorkFlowReviewLogDto> ReviewLogs { get; set; } = new();
        public CustomerGetDto? Customer { get; set; }
    }
}
