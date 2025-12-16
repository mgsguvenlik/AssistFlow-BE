using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.ProgressApprover
{
    public class ProgressApproverGetDto
    {
        public long Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public long CustomerGroupId { get; set; }
        public string? CustomerGroupName { get; set; }   // örn: SubscriberCompany
        public string Phone { get; set; } = string.Empty;
    }
}
