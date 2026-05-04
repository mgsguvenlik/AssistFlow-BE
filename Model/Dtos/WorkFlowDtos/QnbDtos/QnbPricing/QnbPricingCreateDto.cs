using Core.Enums;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbServicesRequestProduct;

namespace Model.Dtos.WorkFlowDtos.QnbDtos.QnbPricing
{
    public class QnbPricingCreateDto
    {
        public string RequestNo { get; set; } = string.Empty;
        public PricingStatus Status { get; set; } = PricingStatus.Pending;
        public string Currency { get; set; } = "TRY";
        public string? Notes { get; set; }
        public decimal TotalAmount { get; set; }

        public ServicesCostStatus ServicesCostStatus { get; set; }

        public List<QnbServicesRequestProductCreateDto>? Products { get; set; }
    }
}