namespace Model.Dtos.WorkFlowDtos.WorkFlowArchive
{
    public class WorkFlowArchiveListDto
    {
        public long Id { get; set; }
        public string RequestNo { get; set; } = default!;
        public string? CustomerName { get; set; }
        public string? TechnicianName { get; set; }
        public string? WorkFlowStatus { get; set; }
        public List<string> WorkOrderTypes { get; set; } = new();
        public string? ServiceTypeName { get; set; }
        public string ArchiveReason { get; set; } = default!;
        public DateTime ArchivedAt { get; set; }
    }

}
