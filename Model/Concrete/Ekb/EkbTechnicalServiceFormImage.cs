using Model.Abstractions;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model.Concrete.Ekb
{

    [Table("EkbTechnicalServiceFormImage", Schema = "ekb")]
    public class EkbTechnicalServiceFormImage : BaseEntity
    {
        [Key]
        public long Id { get; set; }

        [Required]
        public long EkbTechnicalServiceId { get; set; }
        public EkbTechnicalService EkbTechnicalService { get; set; } = default!;

        [Required, MaxLength(500)]
        public string Url { get; set; } = string.Empty;

        [MaxLength(250)]
        public string? Caption { get; set; }
    }
}
