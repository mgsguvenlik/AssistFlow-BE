using Core.Enums;

namespace Model.Dtos.WorkFlowDtos.YkbDtos.YkbCustomerForm
{
    public class YkbCustomerFormCreateDto
    {
        public string RequestNo { get; set; } = string.Empty;
        public string? YkbServiceTrackNo { get; set; }
        public DateTime ServicesDate { get; set; }
        public DateTime? PlannedCompletionDate { get; set; }

        public long CustomerId { get; set; }
        public long? CustomerApproverId { get; set; }

        public string? Description { get; set; }
        public WorkFlowPriority Priority { get; set; } = WorkFlowPriority.Normal;
    }
}
