using Core.Enums;

namespace Model.Dtos.WorkFlowDtos.WorkFlowSlaSetting
{
    public class WorkFlowSlaSettingGetDto
    {
        public long Id { get; set; }
        public WorkFlowCustomerType CustomerType { get; set; }
        public string CustomerTypeName { get; set; } = string.Empty;
        public WorkFlowPriority Priority { get; set; }
        public string PriorityName { get; set; } = string.Empty;
        public int SlaDurationHours { get; set; }
        public int NotificationBeforeHours { get; set; }
        public string? NotificationEmails { get; set; }
        public bool IsActive { get; set; }
        public string? Description { get; set; }
        public DateTimeOffset CreatedDate { get; set; }
        public DateTimeOffset? UpdatedDate { get; set; }
    }
}