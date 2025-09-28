using Model.Abstractions;
using System.ComponentModel.DataAnnotations;

namespace Model.Concrete
{
    public class City : BaseEntity
    {
        [Key]
        public long Id { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = null!; // İl adı

        [MaxLength(10)]
        public string? Code { get; set; }         // Plaka/harici kod (opsiyonel)

        public ICollection<Region> Regions { get; set; } = new List<Region>();
    }
}
