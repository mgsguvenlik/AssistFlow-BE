using Core.Enums;
using Model.Abstractions;
using System.ComponentModel.DataAnnotations;

namespace Model.Concrete.PeriodicReports
{
    public sealed class PeriodicReport : AuditableWithUserEntity
    {
        [Key]
        public long Id { get; set; }

        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        [Required]
        public string SqlQuery { get; set; } = string.Empty;

        public PeriodicReportOutputFormat OutputFormat { get; set; }

        [Required, MaxLength(100)]
        public string CronExpression { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string TimeZoneId { get; set; } = "Europe/Istanbul";

        public bool IsActive { get; set; }
        public DateTimeOffset? NextRunAtUtc { get; set; }
        public DateTimeOffset? LastRunAtUtc { get; set; }
        public DateTimeOffset? LastSuccessAtUtc { get; set; }
        public DateTimeOffset? LastErrorAtUtc { get; set; }

        [MaxLength(4000)]
        public string? LastErrorMessage { get; set; }

        public DateTimeOffset? LeaseExpiresAtUtc { get; set; }

        public ICollection<PeriodicReportRecipient> Recipients { get; set; } = new List<PeriodicReportRecipient>();
        public ICollection<PeriodicReportExecution> Executions { get; set; } = new List<PeriodicReportExecution>();
    }
}
