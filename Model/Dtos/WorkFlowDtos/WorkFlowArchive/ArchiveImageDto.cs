namespace Model.Dtos.WorkFlowDtos.WorkFlowArchive
{
    public class ArchiveImageDto
    {
        public long Id { get; set; }
        public string Url { get; set; } = default!;
        public string Caption { get; set; } = default!;
        public string? Base64 { get; set; }   // burada dosyanın kendisi
    }
}
