using Model.Abstractions;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model.Concrete.Ykb
{
    [Table("YkbTechnicalServiceImage", Schema = "ykb")]
    public class YkbTechnicalServiceImage : BaseEntity
    {
        [Key]
        public long Id { get; set; }

        [Required]
        public long YkbTechnicalServiceId { get; set; }
        public YkbTechnicalService YkbTechnicalService { get; set; } = default!;

        [Required, MaxLength(500)]
        public string Url { get; set; } = string.Empty;

        [MaxLength(250)]
        public string? Caption { get; set; }

    }
}
