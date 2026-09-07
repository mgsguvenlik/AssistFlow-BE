using Model.Abstractions;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model.Concrete.Ekb
{
    [Table("EkbTechnicalServiceWorkSessions", Schema = "ekb")]
    public class EkbTechnicalServiceWorkSession : AuditableWithUserEntity
    {
        [Key]
        public long Id { get; set; }

        public string RequestNo { get; set; } = string.Empty;

        public long WorkFlowId { get; set; }

        public long TechnicalServiceId { get; set; }

        public long CustomerId { get; set; }

        public int SerialNo { get; set; }

        public DateTimeOffset StartedAtUtc { get; set; }

        public DateTimeOffset PlannedEndAtUtc { get; set; }

        public DateTimeOffset? FinishedAtUtc { get; set; }

        public bool IsActive { get; set; }

        public bool IsCompleted { get; set; }

        public int ExtendCount { get; set; }

        public long? ManitouLogSequence { get; set; }

        public bool HasMissingZoneOnFinish { get; set; }

        public string? ReceivedZonesText { get; set; }

        public string? MissingZonesText { get; set; }

        public string? FinishDescription { get; set; }
    }
}
