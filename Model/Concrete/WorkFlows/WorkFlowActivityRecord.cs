using Core.Enums;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model.Concrete.WorkFlows
{
    [Index(nameof(RequestNo))]
    [Index(nameof(WorkFlowId))]
    [Index(nameof(OccurredAtUtc))]
    public class WorkFlowActivityRecord
    {
        [Key] public long Id { get; set; }

        public long? WorkFlowId { get; set; }
        [MaxLength(64)] public string? RequestNo { get; set; }

        public WorkFlowActionType ActionType { get; set; }

        [MaxLength(32)] public string? FromStepCode { get; set; }
        [MaxLength(32)] public string? ToStepCode { get; set; }

        public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

        public long? PerformedByUserId { get; set; }
        [MaxLength(200)] public string? PerformedByUserName { get; set; }

        [MaxLength(45)] public string? ClientIp { get; set; }
        [MaxLength(200)] public string? UserAgent { get; set; }

        [MaxLength(500)] public string? Summary { get; set; }
        public string? PayloadJson { get; set; }

        [MaxLength(64)] public string? CorrelationId { get; set; }

        [ForeignKey(nameof(WorkFlowId))]
        public WorkFlow? WorkFlow { get; set; }
    }
}
