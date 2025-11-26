using Model.Abstractions;
using System.ComponentModel.DataAnnotations;

namespace Model.Concrete.WorkFlows
{
    public class TechnicalServiceImage : BaseEntity
    {
        [Key]
        public long Id { get; set; }

        [Required]
        public long TechnicalServiceId { get; set; }
        public TechnicalService TechnicalService { get; set; } = default!;

        [Required, MaxLength(500)]
        public string Url { get; set; } = string.Empty;

        [MaxLength(250)]
        public string? Caption { get; set; }

    }
}
