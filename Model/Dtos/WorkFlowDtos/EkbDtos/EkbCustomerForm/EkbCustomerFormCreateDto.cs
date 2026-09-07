using Core.Enums;

namespace Model.Dtos.WorkFlowDtos.EkbDtos.EkbCustomerForm
{
    public class EkbCustomerFormCreateDto
    {
        public string RequestNo { get; set; } = string.Empty;
        public string? EkbServiceTrackNo { get; set; }
        public DateTime ServicesDate { get; set; }
        public DateTime? PlannedCompletionDate { get; set; }

        public long CustomerId { get; set; }
        public long? CustomerApproverId { get; set; }

        public string? Title { get; set; }
        public string? Description { get; set; }
        public WorkFlowPriority Priority { get; set; } = WorkFlowPriority.Normal;

        public long ServiceTypeId { get; set; }
        public ServicesRequestStatus ServicesRequestStatus { get; set; }
        public List<long>? WorkOrderTypeIds { get; set; }
    }
}
