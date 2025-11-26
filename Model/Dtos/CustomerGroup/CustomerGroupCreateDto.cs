using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.CustomerGroup
{
    public class CustomerGroupCreateDto
    {
        [Required, MaxLength(200)]
        public string GroupName { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string Code { get; set; } = string.Empty;
        public long? ParentGroupId { get; set; }
        public List<long>? ProgressApproverIds { get; set; }
    }
}
