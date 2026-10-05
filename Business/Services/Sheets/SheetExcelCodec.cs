using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Model.Dtos.Sheets;

namespace Business.Services.Sheets;

public static class SheetExcelCodec
{
    private static readonly Dictionary<uint, string> BuiltinFormats = new()
    {
        [0] = "General", [1] = "0", [2] = "0.00", [3] = "#,##0", [4] = "#,##0.00",
        [9] = "0%", [10] = "0.00%", [11] = "0.00E+00", [14] = "mm-dd-yy",
        [15] = "d-mmm-yy", [16] = "d-mmm", [17] = "mmm-yy", [18] = "h:mm AM/PM",
        [19] = "h:mm:ss AM/PM", [20] = "h:mm", [21] = "h:mm:ss", [22] = "m/d/yy h:mm",
        [37] = "#,##0 ;(#,##0)", [38] = "#,##0 ;[Red](#,##0)", [39] = "#,##0.00;(#,##0.00)",
        [40] = "#,##0.00;[Red](#,##0.00)", [49] = "@"
    };
    public static int ColumnIndex(string reference)
    {
        var col = 0;
        foreach (var c in reference)
        {
            if (c is < 'A' or > 'Z') break;
            col = checked(col * 26 + c - 'A' + 1);
        }
        return col - 1;
    }
    public static string ColumnName(int index)
    {
        var result = "";
        for (var i = index + 1; i > 0; i = (i - 1) / 26) result = (char)('A' + (i - 1) % 26) + result;
        return result;
    }
    private static string? Rgb(string? rgb) => rgb?.Length is 6 or 8 ? "#" + rgb[^6..] : null;
    public static List<SheetCellStyle?> ReadStyles(WorkbookPart workbook)
    {
        var styles = workbook.WorkbookStylesPart?.Stylesheet;
        if (styles?.CellFormats == null) return [null];
        var fonts = styles.Fonts?.Elements<Font>().ToArray() ?? [];
        var fills = styles.Fills?.Elements<Fill>().ToArray() ?? [];
        var formats = styles.NumberingFormats?.Elements<NumberingFormat>().ToDictionary(x => x.NumberFormatId!.Value, x => x.FormatCode!.Value!)
            ?? new Dictionary<uint, string>();
        var themeColors = new List<string?>();
        if (workbook.ThemePart != null)
        {
            using var stream = workbook.ThemePart.GetStream();
            var theme = System.Xml.Linq.XDocument.Load(stream);
            var scheme = theme.Descendants().FirstOrDefault(x => x.Name.LocalName == "clrScheme");
            // Theme indices use light1,dark1,light2,dark2 order.
            var order = new[] { "lt1", "dk1", "lt2", "dk2", "accent1", "accent2", "accent3", "accent4", "accent5", "accent6", "hlink", "folHlink" };
            foreach (var name in order)
            {
                var color = scheme?.Elements().FirstOrDefault(x => x.Name.LocalName == name)?.Elements().FirstOrDefault();
                themeColors.Add(Rgb((string?)color?.Attribute("val") is { Length: 6 } value ? value : (string?)color?.Attribute("lastClr")));
            }
        }
        var indexedColors = styles.Colors?.IndexedColors?.Elements<RgbColor>().Select(x => Rgb(x.Rgb?.Value)).ToArray() ?? [];
        string? ColorValue(ColorType? color)
        {
            if (color == null) return null;
            var rgb = Rgb(color.Rgb?.Value);
            if (rgb == null && color.Theme?.Value is uint t && t < themeColors.Count) rgb = themeColors[(int)t];
            if (rgb == null && color.Indexed?.Value is uint idx)
            {
                if (idx < indexedColors.Length) rgb = indexedColors[idx];
                else if (idx < 64)
                {
                    // Use the existing ClosedXML palette for the standard 64 indexed colors.
                    var indexed = ClosedXML.Excel.XLColor.FromIndex((int)idx).Color;
                    rgb = $"#{indexed.R:X2}{indexed.G:X2}{indexed.B:X2}";
                }
            }
            if (rgb != null && color.Tint?.Value is double tint && tint != 0)
            {
                var channels = Enumerable.Range(0, 3).Select(i => Convert.ToInt32(rgb.Substring(1 + i * 2, 2), 16))
                    .Select(v => (int)Math.Clamp(Math.Round(tint < 0 ? v * (1 + tint) : v * (1 - tint) + 255 * tint), 0, 255));
                rgb = "#" + string.Concat(channels.Select(v => v.ToString("X2")));
            }
            return rgb;
        }
        return styles.CellFormats.Elements<CellFormat>().Select(f =>
        {
            var font = f.FontId?.Value is uint fi && fi < fonts.Length ? fonts[fi] : null;
            var fill = f.FillId?.Value is uint bi && bi < fills.Length ? fills[bi] : null;
            var n = f.NumberFormatId?.Value ?? 0;
            return new SheetCellStyle {
                FontName = font?.FontName?.Val?.Value, FontSize = font?.FontSize?.Val?.Value,
                Bold = font?.Bold != null && font.Bold.Val?.Value != false,
                Italic = font?.Italic != null && font.Italic.Val?.Value != false,
                Color = ColorValue(font?.Color), Background = ColorValue(fill?.PatternFill?.ForegroundColor),
                Alignment = f.Alignment?.Horizontal?.Value.ToString().ToLowerInvariant(),
                Wrap = f.Alignment?.WrapText?.Value ?? false,
                NumberFormat = formats.GetValueOrDefault(n) ?? BuiltinFormats.GetValueOrDefault(n)
            };
        }).Cast<SheetCellStyle?>().ToList();
    }

    // Shared strings are indexed on disk; repeated values do not require an in-memory table.
    public sealed class SharedStrings : IDisposable
    {
        private readonly FileStream data = Temporary();
        private readonly FileStream index = Temporary();
        private readonly BinaryWriter dataWriter;
        private readonly BinaryWriter indexWriter;
        private readonly BinaryReader dataReader;
        private readonly BinaryReader indexReader;
        public SharedStrings(WorkbookPart workbook)
        {
            dataWriter = new(data, System.Text.Encoding.UTF8, true);
            indexWriter = new(index, System.Text.Encoding.UTF8, true);
            dataReader = new(data, System.Text.Encoding.UTF8, true);
            indexReader = new(index, System.Text.Encoding.UTF8, true);
            if (workbook.SharedStringTablePart == null) return;
            using var reader = OpenXmlReader.Create(workbook.SharedStringTablePart);
            while (reader.Read())
                if (reader.IsStartElement && reader.ElementType == typeof(SharedStringItem))
                {
                    var item = (SharedStringItem)reader.LoadCurrentElement();
                    var value = string.Concat(item.Descendants<Text>().Select(x => x.Text));
                    indexWriter.Write(data.Position);
                    dataWriter.Write(value);
                }
            dataWriter.Flush();
            indexWriter.Flush();
        }
        private static FileStream Temporary() => new(Path.Combine(Path.GetTempPath(), "mgs-" + Guid.NewGuid() + ".tmp"),
            FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 65536, FileOptions.DeleteOnClose);
        public string Get(int id)
        {
            if (id < 0 || (long)id * 8 >= index.Length) throw new SheetRuleException("Excel ortak metin indeksi geçersiz.");
            index.Position = (long)id * 8;
            data.Position = indexReader.ReadInt64();
            return dataReader.ReadString();
        }
        public void Dispose()
        {
            dataWriter.Dispose(); indexWriter.Dispose(); dataReader.Dispose(); indexReader.Dispose();
            data.Dispose(); index.Dispose();
        }
    }

    public sealed class StyleCatalog
    {
        private readonly Dictionary<string, uint> ids = new();
        private readonly List<SheetCellStyle> styles = [];
        public uint Get(SheetCellStyle? style)
        {
            if (style == null) return 0;
            var key = SheetSnapshotEngine.Serialize(style);
            if (ids.TryGetValue(key, out var id)) return id;
            if (styles.Count >= 64000) throw new SheetRuleException("Excel biçim sayısı sınırı aşıldı.");
            styles.Add(style); id = (uint)styles.Count; ids[key] = id; return id;
        }
        public Stylesheet Build()
        {
            var fonts = new Fonts(new Font { FontSize = new FontSize { Val = 11 }, FontName = new FontName { Val = "Calibri" } });
            var fills = new Fills(new Fill(new PatternFill { PatternType = PatternValues.None }),
                new Fill(new PatternFill { PatternType = PatternValues.Gray125 }));
            var numbers = new NumberingFormats();
            var cells = new CellFormats(new CellFormat { FontId = 0, FillId = 0, BorderId = 0, NumberFormatId = 0, FormatId = 0 });
            foreach (var style in styles)
            {
                var font = new Font { FontSize = new FontSize { Val = style.FontSize ?? 11 }, FontName = new FontName { Val = style.FontName ?? "Calibri" } };
                if (style.Bold) font.Bold = new Bold();
                if (style.Italic) font.Italic = new Italic();
                if (style.Color != null) font.Color = new Color { Rgb = "FF" + style.Color[1..] };
                fonts.Append(font);
                var fillId = 0u;
                if (style.Background != null)
                {
                    fillId = (uint)fills.ChildElements.Count;
                    fills.Append(new Fill(new PatternFill(new ForegroundColor { Rgb = "FF" + style.Background[1..] },
                        new BackgroundColor { Indexed = 64 }) { PatternType = PatternValues.Solid }));
                }
                var numberId = 0u;
                if (!string.IsNullOrEmpty(style.NumberFormat) && style.NumberFormat != "General")
                {
                    numberId = (uint)(164 + numbers.ChildElements.Count);
                    numbers.Append(new NumberingFormat { NumberFormatId = numberId, FormatCode = style.NumberFormat });
                }
                var format = new CellFormat { FontId = (uint)fonts.ChildElements.Count - 1, FillId = fillId,
                    BorderId = 0, FormatId = 0, NumberFormatId = numberId, ApplyNumberFormat = true,
                    ApplyFont = true, ApplyFill = true, ApplyAlignment = true };
                format.Append(new Alignment { Horizontal = style.Alignment switch {
                    "center" => HorizontalAlignmentValues.Center, "right" => HorizontalAlignmentValues.Right,
                    "justify" => HorizontalAlignmentValues.Justify, _ => HorizontalAlignmentValues.Left }, WrapText = style.Wrap });
                cells.Append(format);
            }
            fonts.Count = (uint)fonts.ChildElements.Count;
            fills.Count = (uint)fills.ChildElements.Count;
            numbers.Count = (uint)numbers.ChildElements.Count;
            cells.Count = (uint)cells.ChildElements.Count;
            return new Stylesheet(fonts, fills, new Borders(new Border()),
                new CellStyleFormats(new CellFormat()), cells,
                new CellStyles(new CellStyle { Name = "Normal", FormatId = 0, BuiltinId = 0 })) {
                NumberingFormats = numbers
            };
        }
    }
    public static StoredSheetCell InheritStyle(WorksheetSnapshot sheet, StoredSheetCell cell)
    {
        var style = cell.Style ?? sheet.RowStyles.GetValueOrDefault(cell.RowId) ?? sheet.ColumnStyles.GetValueOrDefault(cell.ColumnId);
        return style == cell.Style ? cell : new StoredSheetCell {
            RowId = cell.RowId, ColumnId = cell.ColumnId, Value = cell.Value, Kind = cell.Kind, Style = style
        };
    }
    public static bool IsDateFormat(string format)
    {
        var cleaned = System.Text.RegularExpressions.Regex.Replace(format, @"""[^""]*""|\\.|\[[^\]]*\]", "");
        return System.Text.RegularExpressions.Regex.IsMatch(cleaned, "[ydhs]", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    public static SheetCellStyle? ExportStyle(StoredSheetCell cell)
    {
        if (cell.Kind != "date") return cell.Style;
        var style = cell.Style == null ? new SheetCellStyle() :
            SheetSnapshotEngine.Deserialize<SheetCellStyle>(SheetSnapshotEngine.Serialize(cell.Style));
        if (string.IsNullOrEmpty(style.NumberFormat) || style.NumberFormat == "General")
            style.NumberFormat = "yyyy-mm-dd hh:mm:ss";
        return style;
    }

    public static Cell ExportCell(StoredSheetCell cell, int row, int col, StyleCatalog styles)
    {
        var output = new Cell { CellReference = ColumnName(col) + (row + 1), StyleIndex = styles.Get(ExportStyle(cell)) };
        var value = cell.Value ?? "";
        if (cell.Kind == "text" || cell.Value == null)
        {
            output.DataType = CellValues.InlineString;
            output.InlineString = new InlineString(new Text(value) { Space = SpaceProcessingModeValues.Preserve });
        }
        else
        {
            output.DataType = cell.Kind switch { "number" => CellValues.Number, "boolean" => CellValues.Boolean,
                "date" => CellValues.Number, _ => CellValues.String };
            output.CellValue = new CellValue(cell.Kind == "boolean" ? (value == "true" ? "1" : "0") :
                cell.Kind == "date" ? DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToOADate().ToString("R", CultureInfo.InvariantCulture) : value);
        }
        return output;
    }
}
