namespace Model.Dtos.UserFeedbackDtos
{
    /// <summary>
    /// Geri bildirim istatistikleri DTO
    /// </summary>
    public class FeedbackStatisticsDto
    {
        public int TotalFeedbacks { get; set; }
        public int CreatedCount { get; set; }
        public int UnderReviewCount { get; set; }
        public int InProgressCount { get; set; }
        public int CompletedCount { get; set; }
        public int RejectedCount { get; set; }
        public int ClosedCount { get; set; }

        public int SuggestionCount { get; set; }
        public int FeatureRequestCount { get; set; }
        public int BugReportCount { get; set; }
        public int IssueCount { get; set; }
        public int ImprovementCount { get; set; }

        public double AverageResponseTimeHours { get; set; }
        public double CompletionRate { get; set; }
    }
}