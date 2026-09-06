using Core.Enums;
using Core.Enums.Ekb;

namespace Model.Dtos.WorkFlowDtos.EkbDtos.EkbCustomerForm
{
    public class EkbCustomerFormUpdateDto
    {
        public long Id { get; set; }
        public string? EkbServiceTrackNo { get; set; }
        public DateTime ServicesDate { get; set; }
        public DateTime? PlannedCompletionDate { get; set; }

        public long CustomerId { get; set; }
        public long? CustomerApproverId { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public EkbCustomerFormStatus Status { get; set; }
        public WorkFlowPriority Priority { get; set; }
    }
}
