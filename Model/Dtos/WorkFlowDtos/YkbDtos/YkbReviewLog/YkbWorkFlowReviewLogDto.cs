namespace Model.Dtos.WorkFlowDtos.YkbDtos.YkbReviewLog
{
    public class YkbWorkFlowReviewLogDto
    {
        public long Id { get; set; }
        public long YkbWorkFlowId { get; set; }
        public string RequestNo { get; set; } = default!;

        public long? FromStepId { get; set; }
        public string FromStepCode { get; set; } = default!;
        public long? ToStepId { get; set; }
        public string ToStepCode { get; set; } = default!;

        public string ReviewNotes { get; set; } = default!;

        public long CreatedUser { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
