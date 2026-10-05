namespace Model.Dtos.Sheets;

// Physical IDs stay stable across inserts/deletes. A million empty rows need one range.
public sealed class SheetAxisRange
{
    public long Start { get; set; }
    public int Count { get; set; }
}
public sealed class WorksheetSnapshot
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public List<SheetAxisRange> Rows { get; set; } = [];
    public List<SheetAxisRange> Columns { get; set; } = [];
    public long NextRowId { get; set; }
    public long NextColumnId { get; set; }
    public Dictionary<long, List<Guid>> Blocks { get; set; } = [];
    public Dictionary<long, double> ColumnWidths { get; set; } = [];
    public Dictionary<long, double> RowHeights { get; set; } = [];
    public Dictionary<long, SheetCellStyle> ColumnStyles { get; set; } = [];
    public Dictionary<long, SheetCellStyle> RowStyles { get; set; } = [];
}
public sealed class WorkbookSnapshot
{
    public List<WorksheetSnapshot> Worksheets { get; set; } = [];
}
public sealed class StoredSheetCell
{
    public long RowId { get; set; }
    public long ColumnId { get; set; }
    public string? Value { get; set; }
    public string Kind { get; set; } = "text";
    public SheetCellStyle? Style { get; set; }
}
