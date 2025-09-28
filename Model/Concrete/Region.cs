using Model.Abstractions;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model.Concrete
{
    public class Region : BaseEntity
    {
        [Key]
        public long Id { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = null!; // İlçe adı

        [MaxLength(10)]
        public string? Code { get; set; }         // Harici kod (opsiyonel)

        [Required, ForeignKey(nameof(City))]
        public long CityId { get; set; }
        public City City { get; set; } = null!;
    }
}
