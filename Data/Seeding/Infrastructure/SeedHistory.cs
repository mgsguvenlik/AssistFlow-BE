using System.ComponentModel.DataAnnotations;

namespace Data.Seeding.Infrastructure
{
    public class SeedHistory
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string Key { get; set; } = null!;

        public DateTimeOffset AppliedAt { get; set; } = DateTimeOffset.Now;

        [MaxLength(50)]
        public string? Version { get; set; }
    }
}
