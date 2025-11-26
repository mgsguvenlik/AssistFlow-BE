using Core.Enums;
using Model.Dtos.WorkFlowDtos.ServicesRequestProduct;

namespace Model.Dtos.WorkFlowDtos.FinalApproval
{
    public class FinalApprovalUpdateDto
    {
        public string RequestNo { get; set; } = default!;
        public string? Notes { get; set; }
        public WorkFlowStatus WorkFlowStatus { get; set; }
        public FinalApprovalStatus FinalApprovalStatus { get; set; }
        public decimal DiscountPercent { get; set; }
        public List<ServicesRequestProductCreateDto>? Products { get; set; }
    }
}
