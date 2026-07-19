using Microsoft.AspNetCore.Http;

namespace Model.Dtos.WorkFlowDtos.WorkflowAttachment
{
    public class WorkflowAttachmentReplaceDto
    {
        public long AttachmentId { get; set; }

        public IFormFile File { get; set; } = default!;
    }
}