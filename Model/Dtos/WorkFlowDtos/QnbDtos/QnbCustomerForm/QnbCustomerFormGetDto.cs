using Core.Enums;
using Core.Enums.Qnb;
using Model.Dtos.Customer;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbReviewLog;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbServicesRequestProduct;

namespace Model.Dtos.WorkFlowDtos.QnbDtos.QnbCustomerForm
{
    public class QnbCustomerFormGetDto
    {
        public long Id { get; set; }
        public string RequestNo { get; set; } = string.Empty;
        public string? QnbServiceTrackNo { get; set; }
        public DateTime ServicesDate { get; set; }
        public DateTime? PlannedCompletionDate { get; set; }

        public long CustomerId { get; set; }
        public string? CustomerName { get; set; }

        public long? CustomerApproverId { get; set; }
        public string? CustomerApproverName { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public QnbCustomerFormStatus Status { get; set; }
        public WorkFlowPriority Priority { get; set; }

        public DateTimeOffset CreatedDate { get; set; }
        public DateTimeOffset? UpdatedDate { get; set; }
        public long CreatedUser { get; set; }
        public long? UpdatedUser { get; set; }

        public bool IsDeleted { get; set; }

        public List<QnbServicesRequestProductGetDto> ServicesRequestProducts { get; set; } = new();
        public List<QnbWorkFlowReviewLogDto> ReviewLogs { get; set; } = new();
        public CustomerGetDto? Customer { get; set; }
    }
}