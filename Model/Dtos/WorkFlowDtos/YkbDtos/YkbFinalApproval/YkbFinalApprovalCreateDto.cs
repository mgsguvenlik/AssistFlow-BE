using Core.Enums;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbServicesRequestProduct;

namespace Model.Dtos.WorkFlowDtos.YkbDtos.YkbFinalApproval
{
    public class YkbFinalApprovalCreateDto
    {
        public string RequestNo { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public long? DecidedBy { get; set; }
        public decimal DiscountPercent { get; set; }
        public bool? IsAgreement { get; set; }  // Mutabık Kalındı = true, Mutabık Kalınmadı = false
        public WorkFlowStatus WorkFlowStatus { get; set; }
        public FinalApprovalStatus FinalApprovalStatus { get; set; }
        public List<YkbServicesRequestProductCreateDto>? Products { get; set; }

    }
}
