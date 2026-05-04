using Core.Enums;
using Core.Enums.Qnb;

namespace Model.Dtos.WorkFlowDtos.QnbDtos.QnbCustomerForm
{
    public class QnbCustomerFormUpdateDto
    {
        public long Id { get; set; }
        public string? QnbServiceTrackNo { get; set; }
        public DateTime ServicesDate { get; set; }
        public DateTime? PlannedCompletionDate { get; set; }

        public long CustomerId { get; set; }
        public long? CustomerApproverId { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public QnbCustomerFormStatus Status { get; set; }
        public WorkFlowPriority Priority { get; set; }
    }
}