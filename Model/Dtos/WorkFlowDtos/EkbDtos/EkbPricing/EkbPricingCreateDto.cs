using Core.Enums;
using Microsoft.AspNetCore.Http;
using Model.Dtos.WorkFlowDtos.EkbDtos.EkbAttachment;
using Model.Dtos.WorkFlowDtos.EkbDtos.EkbServicesRequestProduct;

namespace Model.Dtos.WorkFlowDtos.EkbDtos.EkbPricing
{
    public class EkbPricingCreateDto
    {
        public string RequestNo { get; set; } = string.Empty;
        public PricingStatus Status { get; set; } = PricingStatus.Pending;
        public string Currency { get; set; } = "TRY";
        public string? Notes { get; set; }
        //public decimal TotalAmount { get; set; }
        public ServicesCostStatus ServicesCostStatus { get; set; }
        public List<EkbServicesRequestProductCreateDto>? Products { get; set; }
        public List<IFormFile>? Attachments { get; set; }

        /// <summary>
        /// Silinecek dosyaların ID listesi.
        /// </summary>
        public List<long>? DeletedAttachmentIds { get; set; }

        /// <summary>
        /// Mevcut bir dosyayı yenisiyle değiştirmek için kullanılır.
        /// </summary>
        public List<EkbWorkflowAttachmentReplaceDto>? ReplacedAttachments { get; set; }

    }
}
