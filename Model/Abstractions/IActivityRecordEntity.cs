using Core.Enums;

namespace Model.Abstractions
{
    public interface IActivityRecordEntity
    {
        long Id { get; set; }
        string? RequestNo { get; set; }
        WorkFlowActionType ActionType { get; set; }

        string? FromStepCode { get; set; }
        string? ToStepCode { get; set; }

        DateTime OccurredAtUtc { get; set; }

        long? PerformedByUserId { get; set; }
        string? PerformedByUserName { get; set; }

        string? ClientIp { get; set; }
        string? UserAgent { get; set; }

        string? CorrelationId { get; set; }

        long? CustomerId { get; set; }

        string? Summary { get; set; }
        string? PayloadJson { get; set; }

        long? WorkFlowId { get; set; }
    }
}
