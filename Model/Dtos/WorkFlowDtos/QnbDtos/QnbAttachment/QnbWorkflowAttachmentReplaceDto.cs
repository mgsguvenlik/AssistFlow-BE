using Microsoft.AspNetCore.Http;

namespace Model.Dtos.WorkFlowDtos.QnbDtos.QnbAttachment
{
    public class QnbWorkflowAttachmentReplaceDto
    {
        public long AttachmentId { get; set; }

        public IFormFile File { get; set; } = default!;
    }
}