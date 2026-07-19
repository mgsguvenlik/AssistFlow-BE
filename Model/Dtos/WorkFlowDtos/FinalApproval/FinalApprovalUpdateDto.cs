using Core.Enums;
using Microsoft.AspNetCore.Http;
using Model.Dtos.WorkFlowDtos.ServicesRequestProduct;
using Model.Dtos.WorkFlowDtos.WorkflowAttachment;

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
        public List<IFormFile>? Attachments { get; set; }

        public List<long>? DeletedAttachmentIds { get; set; }

        public List<WorkflowAttachmentReplaceDto>? ReplacedAttachments { get; set; }
    }
}
