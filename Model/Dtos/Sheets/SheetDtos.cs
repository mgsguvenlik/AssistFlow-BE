using Core.Common;
using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.Sheets;

public static class SheetLimits
{
    public const int Worksheets = 10;
    public const int Rows = 1_000_000;
    public const int Columns = 100;
    public const int BlockRows = 128;
    public const int WindowRows = 256;
    public const int ChangesPerSave = 50_000;
}

public sealed class SheetQuery : QueryParams
{
    public long? OwnerId { get; set; }
    public string? OwnerName { get; set; }
    public bool OwnedOnly { get; set; }
    public DateTime? UpdatedFrom { get; set; }
    public DateTime? UpdatedTo { get; set; }
}

public sealed class SheetCreateDto
{
    [Required(ErrorMessage = "Tablo adı zorunludur."), StringLength(200, ErrorMessage = "Tablo adı en fazla 200 karakter olabilir.")]
    public string Name { get; set; } = "";
    [Range(1, SheetLimits.Rows, ErrorMessage = "Satır sayısı 1 ile 1.000.000 arasında olmalıdır.")]
    public int Rows { get; set; } = 100;
    [Range(1, SheetLimits.Columns, ErrorMessage = "Sütun sayısı 1 ile 100 arasında olmalıdır.")]
    public int Columns { get; set; } = 26;
}

public sealed record SheetListDto(Guid Id, string Name, long OwnerId, string OwnerName,
    DateTime UpdatedAtUtc, bool CanEdit, bool CanManage);
public sealed record SheetUserDto(long Id, string Name);
public sealed record SheetWorkbookDto(Guid Id, string Name, Guid RevisionId, long OwnerId,
    bool CanEdit, bool CanManage, List<WorksheetDto> Worksheets);
public sealed record WorksheetDto(Guid Id, string Name, int Rows, int Columns,
    Dictionary<int, double> ColumnWidths, Dictionary<int, double> RowHeights);

public sealed class SheetCellStyle
{
    public string? FontName { get; set; }
    public double? FontSize { get; set; }
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public string? Color { get; set; }
    public string? Background { get; set; }
    public string? Alignment { get; set; }
    public string? NumberFormat { get; set; }
    public bool Wrap { get; set; }
}

public sealed class SheetCellDto
{
    public int Row { get; set; }
    public int Column { get; set; }
    public string? Value { get; set; }
    public string Kind { get; set; } = "text";
    public SheetCellStyle? Style { get; set; }
}

public sealed class SheetCellChange : SheetCellBase
{
    public Guid WorksheetId { get; set; }
}
public class SheetCellBase
{
    public int Row { get; set; }
    public int Column { get; set; }
    public string? Value { get; set; }
    public string Kind { get; set; } = "text";
    public SheetCellStyle? Style { get; set; }
}

public sealed class SheetStructureChange
{
    public string Type { get; set; } = "";
    public Guid WorksheetId { get; set; }
    public string? Name { get; set; }
    public int Index { get; set; }
    public int Count { get; set; } = 1;
    public int Rows { get; set; } = 100;
    public int Columns { get; set; } = 26;
    public double Size { get; set; }
}
public sealed class SheetSaveDto
{
    public Guid BaseRevisionId { get; set; }
    public List<SheetStructureChange> Structure { get; set; } = [];
    public List<SheetCellChange> Cells { get; set; } = [];
}
public sealed class SheetWindowQuery
{
    public Guid RevisionId { get; set; }
    public Guid WorksheetId { get; set; }
    public int StartRow { get; set; }
    public int Count { get; set; } = 128;
    public List<SheetStructureChange> Structure { get; set; } = [];
}
public sealed record SheetWindowDto(int StartRow, int TotalRows, List<SheetCellDto> Cells)
{
    public Dictionary<int, double> RowHeights { get; init; } = [];
}
public sealed record SheetActivityDto(long Id, long UserId, string UserName, string Action,
    Guid? RevisionId, DateTime OccurredAtUtc, string DetailJson);
