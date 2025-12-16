using Model.Abstractions;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model.Concrete.Ykb
{

    [Table("YkbWorkFlowStep", Schema = "ykb")]
    public class YkbWorkFlowStep: BaseEntity
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
