using Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.PeriodicReports
{
    public sealed class PeriodicReportUpsertDto
    {
        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        [Required]
        public string SqlQuery { get; set; } = string.Empty;

        [Required]
        public PeriodicReportOutputFormat? OutputFormat { get; set; }

        [Required, MaxLength(100)]
        public string CronExpression { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? TimeZoneId { get; set; }

        public bool IsActive { get; set; } = true;

        [Required, MinLength(1)]
        public List<string> RecipientEmails { get; set; } = new();
    }

    public sealed class PeriodicReportPreviewRequestDto
    {
        [Required]
        public string SqlQuery { get; set; } = string.Empty;
    }

    public class PeriodicReportListItemDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public PeriodicReportOutputFormat OutputFormat { get; set; }
        public string CronExpression { get; set; } = string.Empty;
        public string TimeZoneId { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTimeOffset? NextRunAtUtc { get; set; }
        public DateTimeOffset? LastRunAtUtc { get; set; }
        public DateTimeOffset? LastSuccessAtUtc { get; set; }
        public DateTimeOffset? LastErrorAtUtc { get; set; }
        public string? LastErrorMessage { get; set; }
        public PeriodicReportExecutionStatus? LastExecutionStatus { get; set; }
        public int RecipientCount { get; set; }
    }

    public sealed class PeriodicReportDetailDto : PeriodicReportListItemDto
    {
        public string SqlQuery { get; set; } = string.Empty;
        public List<string> RecipientEmails { get; set; } = new();
        public DateTimeOffset CreatedDate { get; set; }
        public long CreatedUser { get; set; }
        public DateTimeOffset? UpdatedDate { get; set; }
        public long? UpdatedUser { get; set; }
    }

    public sealed class PeriodicReportExecutionDto
    {
        public long Id { get; set; }
        public long PeriodicReportId { get; set; }
        public DateTimeOffset StartedAtUtc { get; set; }
        public DateTimeOffset? CompletedAtUtc { get; set; }
        public PeriodicReportExecutionStatus Status { get; set; }
        public int? RowCount { get; set; }
        public PeriodicReportOutputFormat OutputFormat { get; set; }
        public string? FileName { get; set; }
        public long? FileSize { get; set; }
        public int MailRecipientCount { get; set; }
        public string? ErrorMessage { get; set; }
        public PeriodicReportTriggerType TriggerType { get; set; }
        public long? TriggeredByUserId { get; set; }
    }

    public sealed class DynamicReportDataDto
    {
        public List<string> Columns { get; set; } = new();
        public List<Dictionary<string, object?>> Rows { get; set; } = new();
        public bool IsTruncated { get; set; }
        public int RowCount => Rows.Count;
    }

    public sealed class PeriodicReportRunResultDto
    {
        public long? ExecutionId { get; set; }
        public PeriodicReportExecutionStatus? Status { get; set; }
        public string? Message { get; set; }
    }
}
