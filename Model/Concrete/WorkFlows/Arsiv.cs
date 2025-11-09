using Microsoft.EntityFrameworkCore;
using Model.Abstractions;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model.Concrete.WorkFlows
{
    [Table("Arşiv")]                  // <-- tablo adı
    [Index(nameof(RequestNo))]
    [Index(nameof(WorkFlowId))]
    [Index(nameof(ArchivedAtUtc))]
    public class Arsiv : AuditableWithUserEntity
    {
        [Key] public long Id { get; set; }

        [Required, MaxLength(100)]
        public string RequestNo { get; set; } = string.Empty;

        public long? WorkFlowId { get; set; }
        public int Version { get; set; } = 1;

        [MaxLength(500)]
        public string? Summary { get; set; }

        public string SnapshotJson { get; set; } = string.Empty; // nvarchar(max)

        [MaxLength(1000)]
        public string? Notes { get; set; }

        [MaxLength(64)]
        public string? ChecksumSha256 { get; set; }

        public DateTime ArchivedAtUtc { get; set; } = DateTime.UtcNow;
        public long? ArchivedByUserId { get; set; }

        [MaxLength(200)]
        public string? ArchivedByUserName { get; set; }

        public long SizeBytes { get; set; }
    }
}
