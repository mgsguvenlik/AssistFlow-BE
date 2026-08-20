namespace Model.Dtos.UserFeedbackDtos
{
    public class UserFeedbackAttachmentDto
    {
        public long Id { get; set; }
        public long UserFeedbackId { get; set; }
        public string OriginalFileName { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public string Url { get; set; } = string.Empty;
        public long CreatedUser { get; set; }
        public string? CreatedUserName { get; set; }
        public DateTimeOffset CreatedDate { get; set; }
    }
}
