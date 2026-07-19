using Core.Enums;
using Microsoft.AspNetCore.Http;
using Model.Dtos.WorkFlowDtos.ServicesRequestProduct;
using Model.Dtos.WorkFlowDtos.WorkflowAttachment;
using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.WorkFlowDtos.Pricing
{
    public class PricingUpdateDto
    {
        [Required]
        public long Id { get; set; }

        [Required, MaxLength(100)]
        public string RequestNo { get; set; } = string.Empty;

        public PricingStatus Status { get; set; } = PricingStatus.Pending;

        [MaxLength(3)]
        public string Currency { get; set; } = "TRY";

        [MaxLength(1000)]
        public string? Notes { get; set; }

        [Range(0, (double)decimal.MaxValue)]
        public decimal TotalAmount { get; set; } = 0m;

        public ServicesCostStatus ServicesCostStatus { get; set; }

        public List<ServicesRequestProductCreateDto>? Products { get; set; }

        public List<IFormFile>? Attachments { get; set; }

        public List<long>? DeletedAttachmentIds { get; set; }

        public List<WorkflowAttachmentReplaceDto>? ReplacedAttachments { get; set; }
    }
}
