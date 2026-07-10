namespace Model.Dtos.ArchiveExport
{
    public class ArchiveExportSummaryRow
    {
        public long Id { get; set; }
        public string RequestNo { get; set; } = default!;
        public string? CustomerName { get; set; }
        public string? TechnicianName { get; set; }
        public string? WorkFlowStatus { get; set; }
        public string? ServiceTypeName { get; set; }
        public string? WorkOrderTypes { get; set; }
        public string ArchiveReason { get; set; } = default!;
        public DateTime ArchivedAt { get; set; }
    }

    public class ArchiveExportDetailRow
    {
        public string RequestNo { get; set; } = default!;
        public string Section { get; set; } = default!;
        public string Field { get; set; } = default!;
        public string? Value { get; set; }
    }

    public class ArchiveExportImageRow
    {
        public string RequestNo { get; set; } = default!;
        public string ImageGroup { get; set; } = default!;
        public long ImageId { get; set; }
        public string? Caption { get; set; }
        public string? NormalizedUrl { get; set; } // artık zaten normalize gelecek
    }
}