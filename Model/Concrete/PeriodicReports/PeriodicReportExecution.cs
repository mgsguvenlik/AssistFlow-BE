using Core.Enums;
using Model.Abstractions;
using System.ComponentModel.DataAnnotations;

namespace Model.Concrete.PeriodicReports
{
    public sealed class PeriodicReportExecution : BaseEntity
    {
        [Key]
        public long Id { get; set; }
        public long PeriodicReportId { get; set; }
        public DateTimeOffset StartedAtUtc { get; set; }
        public DateTimeOffset? CompletedAtUtc { get; set; }
        public PeriodicReportExecutionStatus Status { get; set; }
        public int? RowCount { get; set; }
        public PeriodicReportOutputFormat OutputFormat { get; set; }

        [MaxLength(260)]
        public string? FileName { get; set; }

        public long? FileSize { get; set; }
        public int MailRecipientCount { get; set; }

        [MaxLength(4000)]
        public string? ErrorMessage { get; set; }

        public PeriodicReportTriggerType TriggerType { get; set; }
        public long? TriggeredByUserId { get; set; }
        public DateTimeOffset CreatedDate { get; set; }

        public PeriodicReport? PeriodicReport { get; set; }
    }
}
