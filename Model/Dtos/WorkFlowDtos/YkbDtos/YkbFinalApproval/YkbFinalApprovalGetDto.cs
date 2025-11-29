using Core.Enums;
using Model.Dtos.Customer;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbReviewLog;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbServicesRequestProduct;

namespace Model.Dtos.WorkFlowDtos.YkbDtos.YkbFinalApproval
{
    public class YkbFinalApprovalGetDto
    {
        public long Id { get; set; }
        public string RequestNo { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public long? DecidedBy { get; set; }
        public FinalApprovalStatus Status { get; set; }
        public decimal DiscountPercent { get; set; }
        public List<YkbWorkFlowReviewLogDto> ReviewLogs { get; set; } = new();
        public List<YkbServicesRequestProductGetDto> Products { get; set; } = new();
        public CustomerGetDto? Customer { get; set; }
    }

}
