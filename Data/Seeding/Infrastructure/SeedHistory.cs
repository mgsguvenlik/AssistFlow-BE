using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Seeding.Infrastructure
{
    public class SeedHistory
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string Key { get; set; } = null!;

        public DateTimeOffset AppliedAt { get; set; } = DateTimeOffset.UtcNow;

        [MaxLength(50)]
        public string? Version { get; set; }
    }
}
