using Model.Abstractions;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model.Concrete.Qnb
{
    [Table("QnbWorkFlowStep", Schema = "qnb")]
    public class QnbWorkFlowStep : BaseEntity
    {
        [Key]
        public long Id { get; set; }

        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? Code { get; set; }
        public int Order { get; set; }
    }
}