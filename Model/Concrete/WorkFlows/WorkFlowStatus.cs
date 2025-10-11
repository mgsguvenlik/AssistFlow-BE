using Model.Abstractions;
using System.ComponentModel.DataAnnotations;

namespace Model.Concrete.WorkFlows
{
    public class WorkFlowStatus : BaseEntity 
    {
        [Key]
        public long Id { get; set; }

        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? Code { get; set; }
    }
}
