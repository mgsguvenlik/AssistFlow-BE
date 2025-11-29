using Core.Enums;
using Model.Dtos.Customer;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbReviewLog;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbServicesRequestProduct;

namespace Model.Dtos.WorkFlowDtos.YkbDtos.YkbPricing
{
    public class YkbPricingGetDto
    {
        public long Id { get; set; }
        public string RequestNo { get; set; } = string.Empty;
        public PricingStatus Status { get; set; }
        public string Currency { get; set; } = "TRY";
        public string? Notes { get; set; }
        public decimal TotalAmount { get; set; }

        public string? OracleNo { get; set; }
        public ServicesCostStatus ServicesCostStatus { get; set; }


        // Audit
        public DateTimeOffset CreatedDate { get; set; }
        public long CreatedUser { get; set; }
        public DateTimeOffset? UpdatedDate { get; set; }
        public long? UpdatedUser { get; set; }
        public List<YkbServicesRequestProductGetDto> Products { get; set; } = new();
        public List<YkbWorkFlowReviewLogDto> ReviewLogs { get; set; } = new();
        public CustomerGetDto? Customer { get; set; }
    }
}
