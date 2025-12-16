namespace Model.Dtos.WorkFlowDtos.YkbDtos.YkbArchive
{
    public class YkbWorkFlowArchiveDetailDto
    {
        public long Id { get; set; }
        public string RequestNo { get; set; } = default!;
        public DateTime ArchivedAt { get; set; }
        public string ArchiveReason { get; set; } = default!;

        public YkbWorkFlowArchiveSnapshotDto Snapshot { get; set; } = default!;
    }
}
