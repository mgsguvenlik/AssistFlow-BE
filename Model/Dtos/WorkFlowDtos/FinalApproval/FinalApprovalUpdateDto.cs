using Core.Enums;

namespace Model.Dtos.WorkFlowDtos.FinalApproval
{
    public class FinalApprovalUpdateDto
    {
        public string RequestNo { get; set; } = default!;
        public string? Notes { get; set; }
        public WorkFlowStatus WorkFlowStatus { get; set; }
        public FinalApprovalStatus FinalApprovalStatus { get; set; }
    }
}
