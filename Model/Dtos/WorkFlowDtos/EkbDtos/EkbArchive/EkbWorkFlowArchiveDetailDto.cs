namespace Model.Dtos.WorkFlowDtos.EkbDtos.EkbArchive
{
    public class EkbWorkFlowArchiveDetailDto
    {
        public long Id { get; set; }
        public string RequestNo { get; set; } = default!;
        public DateTime ArchivedAt { get; set; }
        public string ArchiveReason { get; set; } = default!;

        public EkbWorkFlowArchiveSnapshotDto Snapshot { get; set; } = default!;
    }
}
