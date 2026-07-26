using Core.Enums;
using Model.Dtos.Customer;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbAttachment;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbReviewLog;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbServicesRequestProduct;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbTechnicalServiceImage;

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

        // Resim listeleri
        public List<YkbTechnicalServiceImageGetDto> ServicesImages { get; set; } = new();
        public List<YkbTechnicalServiceFormImageGetDto> ServiceRequestFormImages { get; set; } = new();


        public List<YkbWorkflowAttachmentGetDto> Attachments { get; set; } = new();

        /// <summary>
        /// Frontend dosya ekleme/silme/değiştirme alanlarını buna göre açabilir.
        /// </summary>
        public bool CanEditAttachments { get; set; }

        public string? ProblemDescription { get; set; }
        public string? ResolutionAndActions { get; set; }
    }

}
