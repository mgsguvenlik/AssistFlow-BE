using Core.Enums.Crm;

namespace Model.Dtos.Crm.PurchaseAttachment
{
    public class PurchaseAttachmentCreateDto
    {
        public long PurchaseRequestId { get; set; }

        public PurchaseAttachmentType AttachmentType { get; set; }


        public string OriginalFileName { get; set; } = string.Empty;

        public string StoredFileName { get; set; } = string.Empty;

        public string Extension { get; set; } = string.Empty;

        public string ContentType { get; set; } = "application/octet-stream";

        public long SizeBytes { get; set; }


        /// <summary>
        /// Dosyanın eklendiği CRM step.
        /// Taslakta null olabilir.
        /// </summary>
        public long? UploadedStepId { get; set; }
    }
}