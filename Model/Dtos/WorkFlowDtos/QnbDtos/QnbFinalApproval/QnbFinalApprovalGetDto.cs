using Core.Enums;
using Model.Dtos.Customer;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbAttachment;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbReviewLog;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbServicesRequestProduct;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbTechnicalServiceImage;

namespace Model.Dtos.WorkFlowDtos.QnbDtos.QnbFinalApproval
{
    public class QnbFinalApprovalGetDto
    {
        public long Id { get; set; }
        public string RequestNo { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public long? DecidedBy { get; set; }
        public FinalApprovalStatus Status { get; set; }
        public decimal DiscountPercent { get; set; }
        public List<QnbWorkFlowReviewLogDto> ReviewLogs { get; set; } = new();
        public List<QnbServicesRequestProductGetDto> Products { get; set; } = new();
        public CustomerGetDto? Customer { get; set; }

        public List<QnbTechnicalServiceImageGetDto> ServicesImages { get; set; } = new();
        public List<QnbTechnicalServiceFormImageGetDto> ServiceRequestFormImages { get; set; } = new();

        public List<QnbWorkflowAttachmentGetDto> Attachments { get; set; } = new();
        public bool CanEditAttachments { get; set; }

        public string? ProblemDescription { get; set; }
        public string? ResolutionAndActions { get; set; }
    }
}