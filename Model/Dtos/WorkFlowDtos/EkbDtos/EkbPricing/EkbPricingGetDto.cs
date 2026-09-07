using Core.Enums;
using Model.Dtos.Customer;
using Model.Dtos.WorkFlowDtos.EkbDtos.EkbAttachment;
using Model.Dtos.WorkFlowDtos.EkbDtos.EkbReviewLog;
using Model.Dtos.WorkFlowDtos.EkbDtos.EkbServicesRequestProduct;
using Model.Dtos.WorkFlowDtos.EkbDtos.EkbTechnicalServiceImage;

namespace Model.Dtos.WorkFlowDtos.EkbDtos.EkbPricing
{
    public class EkbPricingGetDto
    {
        public long Id { get; set; }
        public string RequestNo { get; set; } = string.Empty;
        public PricingStatus Status { get; set; }
        public string Currency { get; set; } = "TRY";
        public string? Notes { get; set; }
        //public decimal TotalAmount { get; set; }

        public string? OracleNo { get; set; }
        public ServicesCostStatus ServicesCostStatus { get; set; }


        // Audit
        public DateTimeOffset CreatedDate { get; set; }
        public long CreatedUser { get; set; }
        public DateTimeOffset? UpdatedDate { get; set; }
        public long? UpdatedUser { get; set; }
        public List<EkbServicesRequestProductGetDto> Products { get; set; } = new();
        public List<EkbWorkFlowReviewLogDto> ReviewLogs { get; set; } = new();
        public CustomerGetDto? Customer { get; set; }

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
