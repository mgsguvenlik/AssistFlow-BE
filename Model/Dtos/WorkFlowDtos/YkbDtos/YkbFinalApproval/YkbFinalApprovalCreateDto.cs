using Core.Enums;
using Microsoft.AspNetCore.Http;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbAttachment;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbServicesRequestProduct;

namespace Model.Dtos.WorkFlowDtos.YkbDtos.YkbFinalApproval
{
    public class YkbFinalApprovalCreateDto
    {
        public string RequestNo { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public long? DecidedBy { get; set; }
        public decimal DiscountPercent { get; set; }
        public bool? IsAgreement { get; set; }  // Mutabık Kalındı = true, Mutabık Kalınmadı = false
        public WorkFlowStatus WorkFlowStatus { get; set; }
        public FinalApprovalStatus FinalApprovalStatus { get; set; }
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
