using Model.Abstractions;
using System.ComponentModel.DataAnnotations;

namespace Model.Concrete.PeriodicReports
{
    public sealed class PeriodicReportRecipient : BaseEntity
    {
        [Key]
        public long Id { get; set; }
        public long PeriodicReportId { get; set; }

        [Required, MaxLength(320)]
        public string EmailAddress { get; set; } = string.Empty;

        public PeriodicReport? PeriodicReport { get; set; }
    }
}
