using Core.Enums;
using Model.Dtos.Customer;
using Model.Dtos.WorkFlowDtos.EkbDtos.EkbAttachment;
using Model.Dtos.WorkFlowDtos.EkbDtos.EkbReviewLog;
using Model.Dtos.WorkFlowDtos.EkbDtos.EkbServicesRequestProduct;
using Model.Dtos.WorkFlowDtos.EkbDtos.EkbTechnicalServiceImage;

namespace Model.Dtos.WorkFlowDtos.EkbDtos.EkbFinalApproval
{
    public class EkbFinalApprovalGetDto
    {
        public long Id { get; set; }
        public string RequestNo { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public long? DecidedBy { get; set; }
        public FinalApprovalStatus Status { get; set; }
        public decimal DiscountPercent { get; set; }
        public List<EkbWorkFlowReviewLogDto> ReviewLogs { get; set; } = new();
        public List<EkbServicesRequestProductGetDto> Products { get; set; } = new();
        public CustomerGetDto? Customer { get; set; }

        // Resim listeleri
        public List<EkbTechnicalServiceImageGetDto> ServicesImages { get; set; } = new();
        public List<EkbTechnicalServiceFormImageGetDto> ServiceRequestFormImages { get; set; } = new();


        public List<EkbWorkflowAttachmentGetDto> Attachments { get; set; } = new();

        /// <summary>
        /// Frontend dosya ekleme/silme/değiştirme alanlarını buna göre açabilir.
        /// </summary>
        public bool CanEditAttachments { get; set; }

        public string? ProblemDescription { get; set; }
        public string? ResolutionAndActions { get; set; }
    }

}
