using Core.Enums;

namespace Model.Dtos.WorkFlowDtos.QnbDtos.QnbCustomerForm
{
    public class QnbCustomerFormCreateDto
    {
        public string RequestNo { get; set; } = string.Empty;
        public string? QnbServiceTrackNo { get; set; }
        public DateTime ServicesDate { get; set; }
        public DateTime? PlannedCompletionDate { get; set; }

        public long CustomerId { get; set; }
        public long? CustomerApproverId { get; set; }

        public string? Title { get; set; }
        public string? Description { get; set; }
        public WorkFlowPriority Priority { get; set; } = WorkFlowPriority.Normal;
    }
}