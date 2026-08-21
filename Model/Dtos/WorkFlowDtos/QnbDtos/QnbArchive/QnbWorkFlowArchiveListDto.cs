namespace Model.Dtos.WorkFlowDtos.QnbDtos.QnbArchive
{
    public class QnbWorkFlowArchiveListDto
    {
        public long Id { get; set; }
        public string RequestNo { get; set; } = default!;
        public string? CustomerName { get; set; }
        public string? Name { get; set; }
        public string? WorkFlowStatus { get; set; }
        public string ArchiveReason { get; set; } = default!;
        public DateTime ArchivedAt { get; set; }
    }
}