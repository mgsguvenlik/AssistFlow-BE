namespace Model.Dtos.WorkFlowDtos.QnbDtos.QnbArchive
{
    public class QnbWorkFlowArchiveListDto
    {
        public long Id { get; set; }
        public string RequestNo { get; set; } = default!;
        public string? CustomerName { get; set; }
        public string? TechnicianName { get; set; }
        public string? WorkFlowStatus { get; set; }
        public string ArchiveReason { get; set; } = default!;
        public List<string> WorkOrderTypes { get; set; } = new();  
        public string? ServiceTypeName { get; set; }              
        public DateTime ArchivedAt { get; set; }
    }
}