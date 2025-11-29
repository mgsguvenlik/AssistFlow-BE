using Core.Enums;
using Core.Enums.Ykb;

namespace Model.Dtos.WorkFlowDtos.YkbDtos.YkbCustomerForm
{
    public class YkbCustomerFormUpdateDto
    {
        public long Id { get; set; }
        public string? YkbServiceTrackNo { get; set; }
        public DateTime ServicesDate { get; set; }
        public DateTime? PlannedCompletionDate { get; set; }

        public long CustomerId { get; set; }
        public long? CustomerApproverId { get; set; }

        public string? Description { get; set; }
        public YkbCustomerFormStatus Status { get; set; }
        public WorkFlowPriority Priority { get; set; }
    }
}
