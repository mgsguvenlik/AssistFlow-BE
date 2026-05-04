using Core.Enums;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbServicesRequestProduct;

namespace Model.Dtos.WorkFlowDtos.QnbDtos.QnbFinalApproval
{
    public class QnbFinalApprovalCreateDto
    {
        public string RequestNo { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public long? DecidedBy { get; set; }
        public decimal DiscountPercent { get; set; }
        public bool? IsAgreement { get; set; } // Mutabýk Kalýndý = true, Kalýnmadý = false
        public WorkFlowStatus WorkFlowStatus { get; set; }
        public FinalApprovalStatus FinalApprovalStatus { get; set; }
        public List<QnbServicesRequestProductCreateDto>? Products { get; set; }
    }
}