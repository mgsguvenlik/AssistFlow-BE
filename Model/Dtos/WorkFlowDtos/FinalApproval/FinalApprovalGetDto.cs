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
        public DateTime? DecidedAt { get; set; }
        public FinalApprovalStatus Status { get; set; } = FinalApprovalStatus.Pending;

        // AUDIT (entity’den)
        public DateTime CreatedDate { get; set; }
        public long CreatedUser { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public long? UpdatedUser { get; set; }

        // Review loglar (APR adımı için)
        public List<WorkFlowReviewLogDto> ReviewLogs { get; set; } = new();

        // Ürünler
        public List<ServicesRequestProductGetDto> Products { get; set; } = new();
    }
}
