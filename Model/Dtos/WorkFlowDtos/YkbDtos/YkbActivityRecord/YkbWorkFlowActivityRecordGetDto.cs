using Core.Enums;

namespace Model.Dtos.WorkFlowDtos.YkbDtos.ActivityRecord
{
    public class YkbWorkFlowActivityRecordGetDto
    {
        public long Id { get; set; }
        public string? RequestNo { get; set; }
        public WorkFlowActionType ActionType { get; set; }
        public string? FromStepCode { get; set; }
        public string? ToStepCode { get; set; }

        public DateTime OccurredAtUtc { get; set; }
        public long? PerformedByUserId { get; set; }
        public string? PerformedByUserName { get; set; }
        public string? ClientIp { get; set; }
        public string? UserAgent { get; set; }
        public string? Summary { get; set; }
        public string? PayloadJson { get; set; }
        public string? CorrelationId { get; set; }

        public long? CustomerId { get; set; }
        public string? CustomerName { get; set; }
    }
}
