using Microsoft.AspNetCore.Http;

namespace Model.Dtos.WorkFlowDtos.YkbDtos.YkbAttachment
{
    public class YkbWorkflowAttachmentGetDto
    {
        public long Id { get; set; }

        public string RequestNo { get; set; } = string.Empty;

        public string OriginalFileName { get; set; } = string.Empty;

        public string ContentType { get; set; } = string.Empty;

        public string Extension { get; set; } = string.Empty;

        public long SizeBytes { get; set; }

        public string Url { get; set; } = string.Empty;

        public string UploadedStepCode { get; set; } = string.Empty;

        public string LastUpdatedStepCode { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; }
    }

    public class YkbWorkflowAttachmentReplaceDto
    {
        public long AttachmentId { get; set; }

        public IFormFile File { get; set; } = default!;
    }
}