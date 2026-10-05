using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;
using Model.Dtos.Sheets;

namespace Business.Services.Sheets;

public sealed class SheetRuleException(string message, int status = 400) : Exception(message)
{
    public int Status { get; } = status;
}

public static class SheetSnapshotEngine
{
    private static readonly ConditionalWeakTable<List<SheetAxisRange>, AxisIndex> AxisIndexes = new();
    private sealed class AxisIndex
    {
        private readonly SheetAxisRange[] logical;
        private readonly SheetAxisRange[] physical;
        private readonly int[] offsets;
        private readonly long[] starts;
        private readonly Dictionary<long, int> logicalOffsets;
        public int Total { get; }
        public AxisIndex(List<SheetAxisRange> ranges)
        {
            logical = ranges.ToArray();
            physical = ranges.OrderBy(x => x.Start).ToArray();
            offsets = new int[ranges.Count];
            starts = physical.Select(x => x.Start).ToArray();
            logicalOffsets = new();
            for (var i = 0; i < logical.Length; i++)
            {
                offsets[i] = Total;
                logicalOffsets[logical[i].Start] = Total;
                Total += logical[i].Count;
            }
        }
        public long Physical(int index)
        {
            if (index < 0 || index >= Total) throw new SheetRuleException("Hücre tablo sınırlarının dışında.");
            var segment = Array.BinarySearch(offsets, index);
            if (segment < 0) segment = ~segment - 1;
            return logical[segment].Start + index - offsets[segment];
        }
        public int? Logical(long id)
        {
            var segment = Array.BinarySearch(starts, id);
            if (segment < 0) segment = ~segment - 1;
            if (segment < 0 || id >= physical[segment].Start + physical[segment].Count) return null;
            return logicalOffsets[physical[segment].Start] + (int)(id - physical[segment].Start);
        }
    }
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);
    public static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, JsonOptions)
        ?? throw new SheetRuleException("Tablo verisi okunamadı.");
    public static int Count(List<SheetAxisRange> ranges) => ranges.Sum(x => x.Count);
    public static long Physical(List<SheetAxisRange> ranges, int index) => AxisIndexes.GetValue(ranges, x => new AxisIndex(x)).Physical(index);
    public static int? Logical(List<SheetAxisRange> ranges, long physical) => AxisIndexes.GetValue(ranges, x => new AxisIndex(x)).Logical(physical);
    public static WorksheetSnapshot NewWorksheet(Guid id, string name, int rows, int columns)
    {
        if (id == Guid.Empty || rows < 1 || rows > SheetLimits.Rows || columns < 1 || columns > SheetLimits.Columns)
            throw new SheetRuleException("Çalışma sayfası sınırı: 1.000.000 satır ve 100 sütun.");
        return new WorksheetSnapshot { Id = id, Name = name,
            Rows = [new() { Start = 0, Count = rows }], Columns = [new() { Start = 0, Count = columns }],
            NextRowId = rows, NextColumnId = columns };
    }
    public static void Validate(WorkbookSnapshot snapshot)
    {
        if (snapshot.Worksheets.Count is < 1 or > SheetLimits.Worksheets)
            throw new SheetRuleException("Bir tablo 1–10 çalışma sayfası içermelidir.");
        if (snapshot.Worksheets.Select(x => x.Id).Distinct().Count() != snapshot.Worksheets.Count ||
            snapshot.Worksheets.Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != snapshot.Worksheets.Count)
            throw new SheetRuleException("Çalışma sayfası adı ve kimliği benzersiz olmalıdır.");
        foreach (var sheet in snapshot.Worksheets)
        {
            if (string.IsNullOrWhiteSpace(sheet.Name) || sheet.Name.Length > 31 ||
                sheet.Name.IndexOfAny(['[', ']', ':', '*', '?', '/', '\\']) >= 0 ||
                sheet.Name.StartsWith('\'') || sheet.Name.EndsWith('\''))
                throw new SheetRuleException("Çalışma sayfası adı 1–31 karakter olmalı ve Excel için geçerli olmalıdır.");
            if (Count(sheet.Rows) is < 1 or > SheetLimits.Rows || Count(sheet.Columns) is < 1 or > SheetLimits.Columns)
                throw new SheetRuleException("Çalışma sayfası boyut sınırı aşıldı.");
        }
    }
    private static void Insert(List<SheetAxisRange> ranges, int index, int count, long next, int limit)
    {
        var total = Count(ranges);
        if (count < 1 || index < 0 || index > total || (long)total + count > limit)
            throw new SheetRuleException("Ekleme konumu veya boyut sınırı geçersiz.");
        var position = 0;
        for (var i = 0; i < ranges.Count; i++)
        {
            var range = ranges[i];
            if (index <= position + range.Count)
            {
                var offset = index - position;
                ranges.RemoveAt(i);
                var pieces = new List<SheetAxisRange>();
                if (offset > 0) pieces.Add(new() { Start = range.Start, Count = offset });
                pieces.Add(new() { Start = next, Count = count });
                if (offset < range.Count) pieces.Add(new() { Start = range.Start + offset, Count = range.Count - offset });
                ranges.InsertRange(i, pieces);
                AxisIndexes.Remove(ranges);
                return;
            }
            position += range.Count;
        }
    }
    private static void Delete(List<SheetAxisRange> ranges, int index, int count)
    {
        var total = Count(ranges);
        if (index < 0 || count < 1 || (long)index + count > total || count == total)
            throw new SheetRuleException("Silme konumu geçersiz; en az bir satır/sütun kalmalıdır.");
        var result = new List<SheetAxisRange>();
        var position = 0;
        foreach (var range in ranges)
        {
            var left = Math.Clamp(index - position, 0, range.Count);
            var right = Math.Clamp(index + count - position, 0, range.Count);
            if (left > 0) result.Add(new() { Start = range.Start, Count = left });
            if (right < range.Count) result.Add(new() { Start = range.Start + right, Count = range.Count - right });
            position += range.Count;
        }
        ranges.Clear();
        ranges.AddRange(result);
        AxisIndexes.Remove(ranges);
    }
    public static void ApplyStructure(WorkbookSnapshot snapshot, IEnumerable<SheetStructureChange> changes)
    {
        foreach (var change in changes)
        {
            if (change.Type == "addSheet")
            {
                snapshot.Worksheets.Add(NewWorksheet(change.WorksheetId, change.Name?.Trim() ?? "", change.Rows, change.Columns));
                Validate(snapshot);
                continue;
            }
            var sheet = snapshot.Worksheets.SingleOrDefault(x => x.Id == change.WorksheetId)
                ?? throw new SheetRuleException("Çalışma sayfası bulunamadı.");
            switch (change.Type)
            {
                case "renameSheet": sheet.Name = change.Name?.Trim() ?? ""; break;
                case "removeSheet": snapshot.Worksheets.Remove(sheet); break;
                case "moveSheet":
                    if (change.Index < 0 || change.Index >= snapshot.Worksheets.Count) throw new SheetRuleException("Çalışma sayfası sırası geçersiz.");
                    snapshot.Worksheets.Remove(sheet);
                    snapshot.Worksheets.Insert(change.Index, sheet);
                    break;
                case "insertRows": Insert(sheet.Rows, change.Index, change.Count, sheet.NextRowId, SheetLimits.Rows); sheet.NextRowId += change.Count; break;
                case "insertColumns": Insert(sheet.Columns, change.Index, change.Count, sheet.NextColumnId, SheetLimits.Columns); sheet.NextColumnId += change.Count; break;
                case "deleteRows": Delete(sheet.Rows, change.Index, change.Count); break;
                case "deleteColumns": Delete(sheet.Columns, change.Index, change.Count); break;
                case "columnWidth":
                    if (!double.IsFinite(change.Size) || change.Size is < 10 or > 1000) throw new SheetRuleException("Sütun genişliği geçersiz.");
                    sheet.ColumnWidths[Physical(sheet.Columns, change.Index)] = change.Size; break;
                case "rowHeight":
                    if (!double.IsFinite(change.Size) || change.Size is < 10 or > 1000) throw new SheetRuleException("Satır yüksekliği geçersiz.");
                    sheet.RowHeights[Physical(sheet.Rows, change.Index)] = change.Size; break;
                default: throw new SheetRuleException("Desteklenmeyen yapı işlemi.");
            }
            Validate(snapshot);
        }
    }
    public static void ValidateCell(SheetCellBase cell)
    {
        if (cell.Value?.Length > 32767) throw new SheetRuleException("Hücre metni en fazla 32767 karakter olabilir.");
        if (!new[] { "text", "number", "boolean", "date" }.Contains(cell.Kind)) throw new SheetRuleException("Hücre veri tipi geçersiz.");
        if (!string.IsNullOrEmpty(cell.Value))
        {
            if (cell.Kind == "number" && (!double.TryParse(cell.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number)))
                throw new SheetRuleException("Sayısal hücre geçersiz.");
            if (cell.Kind == "boolean" && cell.Value != "true" && cell.Value != "false") throw new SheetRuleException("Mantıksal hücre değeri geçersiz.");
            if (cell.Kind == "date" && !DateTime.TryParse(cell.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _))
                throw new SheetRuleException("Tarih hücresi geçersiz.");
        }
        var style = cell.Style;
        if (style == null) return;
        if (style.FontName?.Length > 100 || style.NumberFormat?.Length > 200 ||
            (style.FontSize.HasValue && (!double.IsFinite(style.FontSize.Value) || style.FontSize is < 1 or > 409)))
            throw new SheetRuleException("Hücre biçimi geçersiz.");
        foreach (var color in new[] { style.Color, style.Background })
            if (color != null && !Regex.IsMatch(color, "^#[0-9a-fA-F]{6}$"))
                throw new SheetRuleException("Renk #RRGGBB biçiminde olmalıdır.");
        if (style.Alignment != null && !new[] { "left", "center", "right", "justify" }.Contains(style.Alignment))
            throw new SheetRuleException("Hizalama geçersiz.");
    }
    public static WorksheetDto ToDto(WorksheetSnapshot sheet) => new(sheet.Id, sheet.Name, Count(sheet.Rows), Count(sheet.Columns),
        sheet.ColumnWidths.Where(x => Logical(sheet.Columns, x.Key).HasValue).ToDictionary(x => Logical(sheet.Columns, x.Key)!.Value, x => x.Value),
        sheet.RowHeights.Where(x => Logical(sheet.Rows, x.Key) is int row && row < SheetLimits.WindowRows)
            .ToDictionary(x => Logical(sheet.Rows, x.Key)!.Value, x => x.Value));
}
