using Model.Abstractions;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model.Concrete.Qnb
{
    [Table("QnbTechnicalServiceFormImage", Schema = "qnb")]
    public class QnbTechnicalServiceFormImage : BaseEntity
    {
        [Key]
        public long Id { get; set; }

        public long QnbTechnicalServiceId { get; set; }

        [ForeignKey(nameof(QnbTechnicalServiceId))]
        public QnbTechnicalService QnbTechnicalService { get; set; } = default!;

        [Required, MaxLength(500)]
        public string Url { get; set; } = string.Empty;

        [MaxLength(250)]
        public string? Caption { get; set; }
    }
}