using Model.Dtos.CustomerGroupProductPrice;
using Model.Dtos.ProgressApprover;

namespace Model.Dtos.CustomerGroup
{
    public class CustomerGroupGetDto
    {
        public long Id { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public long? ParentGroupId { get; set; }
        public string? ParentGroupName { get; set; }

        public List<CustomerGroupChildDto> SubGroups { get; set; } = new();
        public List<CustomerGroupProductPriceGetDto> GroupProductPrices { get; set; } = new();
        public List<ProgressApproverGetDto> ProgressApprovers { get; set; } = new();
    }
}
