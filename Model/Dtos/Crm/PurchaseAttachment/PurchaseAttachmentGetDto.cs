using Core.Enums.Crm;

namespace Model.Dtos.Crm.PurchaseAttachment
{
    public class PurchaseAttachmentGetDto
    {
        public long Id { get; set; }

        public long PurchaseRequestId { get; set; }

        public PurchaseAttachmentType AttachmentType { get; set; }

        public string? AttachmentTypeName { get; set; }

        public string OriginalFileName { get; set; } = string.Empty;

        public string StoredFileName { get; set; } = string.Empty;

        public string Extension { get; set; } = string.Empty;

        public string ContentType { get; set; } = string.Empty;

        public long SizeBytes { get; set; }


        public long? UploadedStepId { get; set; }

        public string? UploadedStepCode { get; set; }

        public string? UploadedStepName { get; set; }


        public long CreatedUser { get; set; }

        public string? CreatedUserName { get; set; }

        public DateTimeOffset CreatedDate { get; set; }
    }
}