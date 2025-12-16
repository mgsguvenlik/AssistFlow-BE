using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.CustomerGroup
{
    public class CustomerGroupUpdateDto
    {
        [Required]
        public long Id { get; set; }

        [MaxLength(200)]
        public string? GroupName { get; set; }

        [MaxLength(50)]
        public string? Code { get; set; }

        public long? ParentGroupId { get; set; }
        public List<long>? ProgressApproverIds { get; set; }
    }
}
