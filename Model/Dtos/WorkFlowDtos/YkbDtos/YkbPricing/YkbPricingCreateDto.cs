using Core.Enums;
using Microsoft.AspNetCore.Http;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbAttachment;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbServicesRequestProduct;

namespace Model.Dtos.WorkFlowDtos.YkbDtos.YkbPricing
{
    public class YkbPricingCreateDto
    {
        public string RequestNo { get; set; } = string.Empty;
        public PricingStatus Status { get; set; } = PricingStatus.Pending;
        public string Currency { get; set; } = "TRY";
        public string? Notes { get; set; }
        public decimal TotalAmount { get; set; }
        public ServicesCostStatus ServicesCostStatus { get; set; }
        public List<YkbServicesRequestProductCreateDto>? Products { get; set; }
        public List<IFormFile>? Attachments { get; set; }

        /// <summary>
        /// Silinecek dosyaların ID listesi.
        /// </summary>
        public List<long>? DeletedAttachmentIds { get; set; }

        /// <summary>
        /// Mevcut bir dosyayı yenisiyle değiştirmek için kullanılır.
        /// </summary>
        public List<YkbWorkflowAttachmentReplaceDto>? ReplacedAttachments { get; set; }

    }
}
