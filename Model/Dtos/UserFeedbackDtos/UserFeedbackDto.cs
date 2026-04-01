using Core.Enums;

namespace Model.Dtos.UserFeedbackDtos
{
    /// <summary>
    /// Kullanıcı geri bildirimi görüntüleme DTO
    /// </summary>
    public class UserFeedbackDto
    {
        public long Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public FeedbackType FeedbackType { get; set; }
        public string FeedbackTypeText { get; set; } = string.Empty;
        public FeedbackStatus Status { get; set; }
        public string StatusText { get; set; } = string.Empty;
        public int Priority { get; set; }
        public string? AdminResponse { get; set; }
        public DateTimeOffset? ResponseDate { get; set; }
        public long? RespondedBy { get; set; }
        public string? RespondedByName { get; set; }
        public DateTimeOffset? CompletedDate { get; set; }
        public string? RelatedUrl { get; set; }
        public string? UserAgent { get; set; }
        public List<string>? AttachmentUrls { get; set; }

        // Audit bilgileri
        public long CreatedUser { get; set; }
        public string? CreatedUserName { get; set; }
        public DateTimeOffset CreatedDate { get; set; }
        public DateTimeOffset? UpdatedDate { get; set; }
    }
}