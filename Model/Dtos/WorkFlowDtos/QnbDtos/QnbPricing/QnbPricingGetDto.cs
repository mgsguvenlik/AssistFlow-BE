using Core.Enums;
using Model.Dtos.Customer;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbAttachment;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbReviewLog;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbServicesRequestProduct;

namespace Model.Dtos.WorkFlowDtos.QnbDtos.QnbPricing
{
    public class QnbPricingGetDto
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
        public List<QnbServicesRequestProductGetDto> Products { get; set; } = new();
        public List<QnbWorkFlowReviewLogDto> ReviewLogs { get; set; } = new();
        public CustomerGetDto? Customer { get; set; }

        public List<QnbWorkflowAttachmentGetDto> Attachments { get; set; } = new();
        public bool CanEditAttachments { get; set; }


        public string? ProblemDescription { get; set; }
        public string? ResolutionAndActions { get; set; }
    }
}