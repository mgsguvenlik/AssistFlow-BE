using Core.Enums;
using Model.Dtos.WorkFlowDtos.ServicesRequestProduct;
using Model.Dtos.WorkFlowDtos.WorkFlowReviewLog;

namespace Model.Dtos.WorkFlowDtos.FinalApproval
{
    public class FinalApprovalGetDto
    {
        public long Id { get; set; }
        public string RequestNo { get; set; } = default!;
        public string? Notes { get; set; }

        public long? DecidedBy { get; set; }
        public FinalApprovalStatus Status { get; set; } = FinalApprovalStatus.Pending;

        // Review loglar (APR adımı için)
        public List<WorkFlowReviewLogDto> ReviewLogs { get; set; } = new();

        // Ürünler
        public List<ServicesRequestProductGetDto> Products { get; set; } = new();
    }
}
