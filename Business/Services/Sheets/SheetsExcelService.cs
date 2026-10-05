using System.Data;
using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.EntityFrameworkCore;
using Model.Concrete.Sheets;
using Model.Dtos.Sheets;
using static Business.Services.Sheets.SheetSnapshotEngine;

namespace Business.Services.Sheets;

public sealed partial class SheetsService
{
    public async Task<SheetWorkbookDto> CreateFromExcelAsync(string name, Stream file, CancellationToken ct)
    {
        var book = await CreateAsync(new SheetCreateDto { Name = name, Rows = 1, Columns = 1 }, ct);
        try
        {
            var imported = await ImportCoreAsync(book.Id, new SheetSaveDto { BaseRevisionId = book.RevisionId }, file, true, ct);
            return await SaveAsync(book.Id, new SheetSaveDto { BaseRevisionId = imported.RevisionId }, ct);
        }
        catch
        {
            // Remove the newly created workbook and all drafts if initial import fails.
            db.ChangeTracker.Clear();
            await db.Set<SheetWorkbook>().Where(x => x.Id == book.Id).ExecuteDeleteAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<SheetWorkbookDto> ImportAsync(Guid id, SheetSaveDto draft, Stream file, CancellationToken ct)
        => await ImportCoreAsync(id, draft, file, false, ct);

    private async Task<SheetWorkbookDto> ImportCoreAsync(Guid id, SheetSaveDto draft, Stream file, bool replacePlaceholder, CancellationToken ct)
    {
        await Require(id, "edit", ct);
        // First preserve the editor's changes privately, without publishing an active revision.
        var staged = await SaveCoreAsync(id, draft, false, ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var book = await db.Set<SheetWorkbook>().FromSqlInterpolated(
            $"SELECT * FROM [sheets].[Workbooks] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={id}").SingleOrDefaultAsync(ct)
            ?? throw new SheetRuleException("Tablo bulunamadı.", 404);
        await Require(id, "edit", ct);
        var snapshot = Deserialize<WorkbookSnapshot>((await Revision(id, staged.RevisionId, ct)).SnapshotJson);
        if (replacePlaceholder) snapshot.Worksheets.Clear();
        try
        {
            using var document = SpreadsheetDocument.Open(file, false);
            var workbook = document.WorkbookPart ?? throw new SheetRuleException("Excel çalışma kitabı bulunamadı.");
            var imported = workbook.Workbook.Sheets?.Elements<Sheet>().ToList() ?? [];
            if (imported.Count == 0 || snapshot.Worksheets.Count + imported.Count > SheetLimits.Worksheets)
                throw new SheetRuleException("Excel eklendiğinde tablo en fazla 10 çalışma sayfası içerebilir.");
            var styles = SheetExcelCodec.ReadStyles(workbook);
            using var strings = new SheetExcelCodec.SharedStrings(workbook);
            var date1904 = workbook.Workbook.WorkbookProperties?.Date1904?.Value ?? false;
            foreach (var source in imported)
            {
                ct.ThrowIfCancellationRequested();
                if (source.Id?.Value == null || workbook.GetPartById(source.Id.Value) is not WorksheetPart part)
                    throw new SheetRuleException("Desteklenmeyen Excel çalışma sayfası türü.");
                var originalName = source.Name?.Value ?? "Sayfa";
                var name = originalName;
                for (var suffix = 2; snapshot.Worksheets.Any(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)); suffix++)
                {
                    var tail = " (" + suffix + ")";
                    name = originalName[..Math.Min(originalName.Length, 31 - tail.Length)] + tail;
                }
                var sheet = NewWorksheet(Guid.NewGuid(), name, 1, 1);
                var maxRows = 1;
                var maxColumns = 1;
                var blockNumber = -1L;
                var blockCells = new List<StoredSheetCell>();
                async Task Flush()
                {
                    if (blockCells.Count == 0) return;
                    var ids = await StoreCellsAsync(id, blockCells, ct);
                    if (!sheet.Blocks.TryGetValue(blockNumber, out var existing))
                        sheet.Blocks[blockNumber] = ids;
                    else existing.AddRange(ids);
                    blockCells.Clear();
                }
                using var reader = OpenXmlReader.Create(part);
                var priorRow = -1;
                while (reader.Read())
                {
                    ct.ThrowIfCancellationRequested();
                    if (!reader.IsStartElement) continue;
                    if (reader.ElementType == typeof(SheetDimension))
                    {
                        var dimension = (SheetDimension)reader.LoadCurrentElement();
                        var end = dimension.Reference?.Value?.Split(':').Last();
                        if (end != null)
                        {
                            var columns = SheetExcelCodec.ColumnIndex(end) + 1;
                            var rowText = new string(end.Where(char.IsDigit).ToArray());
                            if (!int.TryParse(rowText, out var rows) || rows < 1 || rows > SheetLimits.Rows || columns < 1 || columns > SheetLimits.Columns)
                                throw new SheetRuleException("Excel çalışma sayfası boyut sınırı aşıldı.");
                            maxRows = rows; maxColumns = columns;
                        }
                        continue;
                    }
                    if (reader.ElementType == typeof(Column))
                    {
                        var column = (Column)reader.LoadCurrentElement();
                        var min = (int)(column.Min?.Value ?? 1) - 1;
                        var max = (int)(column.Max?.Value ?? 1);
                        // Excel may assign default widths to all 16,384 columns; only the module's 100 are retained.
                        for (var c = Math.Max(0, min); c < Math.Min(max, SheetLimits.Columns); c++)
                        {
                            if (column.Width?.Value is double width) sheet.ColumnWidths[c] = Math.Clamp(width * 7 + 5, 10, 1000);
                            if (column.Style?.Value is uint styleId && styleId > 0 && styleId < styles.Count && styles[(int)styleId] is { } columnStyle)
                                sheet.ColumnStyles[c] = columnStyle;
                        }
                        continue;
                    }
                    if (reader.ElementType != typeof(Row)) continue;
                    var row = (Row)reader.LoadCurrentElement();
                    var rowIndex = (int)(row.RowIndex?.Value ?? (uint)(priorRow + 2)) - 1;
                    if (rowIndex < 0 || rowIndex >= SheetLimits.Rows || rowIndex <= priorRow)
                        throw new SheetRuleException("Excel satırları sıralı olmalı ve 1.000.000 sınırını aşmamalıdır.");
                    priorRow = rowIndex;
                    maxRows = Math.Max(maxRows, rowIndex + 1);
                    if (row.Height?.Value is double height) sheet.RowHeights[rowIndex] = Math.Clamp(height * 96 / 72, 10, 1000);
                    if (row.StyleIndex?.Value is uint rowStyleId && rowStyleId > 0 && rowStyleId < styles.Count && styles[(int)rowStyleId] is { } rowStyle)
                        sheet.RowStyles[rowIndex] = rowStyle;
                    var nextBlock = rowIndex / SheetLimits.BlockRows;
                    if (blockNumber != nextBlock) { await Flush(); blockNumber = nextBlock; }
                    var nextColumn = 0;
                    foreach (var input in row.Elements<Cell>())
                    {
                        var col = input.CellReference?.Value is string reference ? SheetExcelCodec.ColumnIndex(reference) : nextColumn;
                        nextColumn = col + 1;
                        if (col < 0 || col >= SheetLimits.Columns) throw new SheetRuleException("Excel sütun sınırı 100'dür.");
                        maxColumns = Math.Max(maxColumns, col + 1);
                        var styleIndex = input.StyleIndex?.Value;
                        var style = styleIndex is uint si && si < styles.Count ? styles[(int)si] :
                            sheet.RowStyles.GetValueOrDefault(rowIndex) ?? sheet.ColumnStyles.GetValueOrDefault(col) ?? styles[0];
                        if (style?.Alignment != null && !new[] { "left", "center", "right", "justify" }.Contains(style.Alignment)) style.Alignment = "left";
                        var value = input.CellValue?.Text;
                        var kind = "number";
                        if (input.DataType?.Value == CellValues.SharedString)
                        {
                            if (!int.TryParse(value, out var stringId)) throw new SheetRuleException("Excel metin hücresi geçersiz.");
                            value = strings.Get(stringId); kind = "text";
                        }
                        else if (input.DataType?.Value == CellValues.InlineString)
                        {
                            value = string.Concat(input.InlineString?.Descendants<Text>().Select(x => x.Text) ?? []); kind = "text";
                        }
                        else if (input.DataType?.Value == CellValues.Boolean) { kind = "boolean"; value = value == "1" ? "true" : "false"; }
                        else if (input.DataType?.Value == CellValues.Date) kind = "date";
                        else if (input.DataType?.Value == CellValues.String || input.DataType?.Value == CellValues.Error) kind = "text";
                        // Formula expressions are omitted; their cached values are imported when available.
                        if (value == null && style == null) continue;
                        if (value == null) kind = "text";
                        if (kind == "number" && !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _)) kind = "text";
                        if (date1904 && kind == "number" && style?.NumberFormat is string nf &&
                            SheetExcelCodec.IsDateFormat(nf) &&
                            double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial))
                            value = (serial + 1462).ToString("R", CultureInfo.InvariantCulture);
                        ValidateCell(new SheetCellBase { Value = value, Kind = kind, Style = style });
                        blockCells.Add(new() { RowId = rowIndex, ColumnId = col, Value = value, Kind = kind, Style = style });
                        if (blockCells.Count >= 256) await Flush();
                    }
                }
                await Flush();
                sheet.Rows = [new() { Start = 0, Count = maxRows }];
                sheet.Columns = [new() { Start = 0, Count = maxColumns }];
                sheet.NextRowId = maxRows; sheet.NextColumnId = maxColumns;
                snapshot.Worksheets.Add(sheet);
                Validate(snapshot);
            }
        }
        catch (SheetRuleException) { throw; }
        catch (Exception ex) when (ex is OpenXmlPackageException or System.IO.InvalidDataException or FormatException or OverflowException)
        { throw new SheetRuleException("Excel dosyası okunamadı veya desteklenmeyen veri içeriyor."); }
        var revision = new SheetRevision { Id = Guid.NewGuid(), WorkbookId = id, BaseRevisionId = staged.RevisionId,
            CreatedAtUtc = DateTime.UtcNow, CreatedBy = current.Id, IsDraft = true, SnapshotJson = Serialize(snapshot) };
        uow.Repository.Add(revision);
        Log(id, "import", revision.Id, new { worksheets = snapshot.Worksheets.Count });
        await uow.Repository.CompleteAsync(ct);
        await tx.CommitAsync(ct);
        return await Dto(book, revision, ct);
    }

    public async Task<string> ExportAsync(Guid id, Guid? revisionId, CancellationToken ct)
    {
        var book = await Require(id, "edit", ct);
        var revision = await Revision(id, revisionId ?? book.ActiveRevisionId, ct);
        var snapshot = Deserialize<WorkbookSnapshot>(revision.SnapshotJson);
        var path = Path.Combine(Path.GetTempPath(), "mgs-" + Guid.NewGuid() + ".xlsx");
        try
        {
            var styles = new SheetExcelCodec.StyleCatalog();
            // Collect only distinct styles, reading bounded batches of immutable blocks.
            foreach (var sheet in snapshot.Worksheets)
            {
                foreach (var item in sheet.ColumnStyles.Where(x => Logical(sheet.Columns, x.Key).HasValue)) styles.Get(item.Value);
                foreach (var item in sheet.RowStyles.Where(x => Logical(sheet.Rows, x.Key).HasValue)) styles.Get(item.Value);
                foreach (var batch in sheet.Blocks.Values.SelectMany(x => x).Distinct().Chunk(32))
                    await foreach (var block in Query<SheetDataBlock>().AsNoTracking().Where(x => x.WorkbookId == id && batch.Contains(x.Id)).AsAsyncEnumerable().WithCancellation(ct))
                        foreach (var cell in Deserialize<List<StoredSheetCell>>(block.CellsJson))
                            if (Logical(sheet.Rows, cell.RowId).HasValue && Logical(sheet.Columns, cell.ColumnId).HasValue)
                                styles.Get(SheetExcelCodec.ExportStyle(SheetExcelCodec.InheritStyle(sheet, cell)));
            }
            using (var document = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook))
            {
                var workbook = document.AddWorkbookPart();
                workbook.Workbook = new Workbook(new WorkbookProperties { Date1904 = false }, new DocumentFormat.OpenXml.Spreadsheet.Sheets());
                var stylePart = workbook.AddNewPart<WorkbookStylesPart>();
                stylePart.Stylesheet = styles.Build();
                stylePart.Stylesheet.Save();
                var sheets = workbook.Workbook.GetFirstChild<DocumentFormat.OpenXml.Spreadsheet.Sheets>()!;
                uint sheetId = 1;
                foreach (var sheet in snapshot.Worksheets)
                {
                    var part = workbook.AddNewPart<WorksheetPart>();
                    sheets.Append(new Sheet { Id = workbook.GetIdOfPart(part), SheetId = sheetId++, Name = sheet.Name });
                    using var writer = OpenXmlWriter.Create(part);
                    writer.WriteStartElement(new Worksheet());
                    writer.WriteElement(new SheetDimension { Reference = "A1:" + SheetExcelCodec.ColumnName(Count(sheet.Columns) - 1) + Count(sheet.Rows) });
                    if (sheet.ColumnWidths.Count > 0 || sheet.ColumnStyles.Count > 0)
                    {
                        writer.WriteStartElement(new Columns());
                        foreach (var entry in sheet.ColumnWidths.Keys.Union(sheet.ColumnStyles.Keys).Select(key => (Key: key, Index: Logical(sheet.Columns, key))).Where(x => x.Index.HasValue).OrderBy(x => x.Index))
                        {
                            var column = new Column { Min = (uint)entry.Index!.Value + 1, Max = (uint)entry.Index.Value + 1 };
                            if (sheet.ColumnWidths.TryGetValue(entry.Key, out var width)) { column.Width = Math.Max(0.1, (width - 5) / 7); column.CustomWidth = true; }
                            if (sheet.ColumnStyles.TryGetValue(entry.Key, out var columnStyle)) column.Style = styles.Get(columnStyle);
                            writer.WriteElement(column);
                        }
                        writer.WriteEndElement();
                    }
                    writer.WriteStartElement(new SheetData());
                    long cachedBlock = -1;
                    Dictionary<long, List<StoredSheetCell>> rows = [];
                    var oversized = false;
                    var rowCount = Count(sheet.Rows);
                    for (var r = 0; r < rowCount; r++)
                    {
                        ct.ThrowIfCancellationRequested();
                        var physical = Physical(sheet.Rows, r);
                        var blockNumber = physical / SheetLimits.BlockRows;
                        if (blockNumber != cachedBlock)
                        {
                            cachedBlock = blockNumber; rows.Clear();
                            oversized = false;
                            if (sheet.Blocks.TryGetValue(blockNumber, out var blockIds))
                            {
                                var length = await Query<SheetDataBlock>().Where(x => x.WorkbookId == id && blockIds.Contains(x.Id))
                                    .SumAsync(x => (long)x.CellsJson.Length, ct);
                                oversized = length > 4 * 1024 * 1024;
                                if (!oversized)
                                {
                                    await foreach (var block in Query<SheetDataBlock>().AsNoTracking().Where(x => x.WorkbookId == id && blockIds.Contains(x.Id)).AsAsyncEnumerable().WithCancellation(ct))
                                        foreach (var cell in Deserialize<List<StoredSheetCell>>(block.CellsJson))
                                        {
                                            if (!rows.TryGetValue(cell.RowId, out var list)) rows[cell.RowId] = list = [];
                                            list.Add(cell);
                                        }
                                }
                            }
                        }
                        var present = rows.GetValueOrDefault(physical) ?? [];
                        if (oversized)
                        {
                            var blockIds = sheet.Blocks[blockNumber];
                            await foreach (var block in Query<SheetDataBlock>().AsNoTracking().Where(x => x.WorkbookId == id && blockIds.Contains(x.Id)).AsAsyncEnumerable().WithCancellation(ct))
                                present.AddRange(Deserialize<List<StoredSheetCell>>(block.CellsJson).Where(x => x.RowId == physical));
                        }
                        var values = present.Select(x => (Cell: x, Column: Logical(sheet.Columns, x.ColumnId)))
                            .Where(x => x.Column.HasValue).OrderBy(x => x.Column).ToArray();
                        var customHeight = sheet.RowHeights.TryGetValue(physical, out var height);
                        var customStyle = sheet.RowStyles.TryGetValue(physical, out var rowStyle);
                        if (values.Length == 0 && !customHeight && !customStyle) continue;
                        var row = new Row { RowIndex = (uint)r + 1 };
                        if (customHeight) { row.Height = height * 72 / 96; row.CustomHeight = true; }
                        if (customStyle) { row.StyleIndex = styles.Get(rowStyle); row.CustomFormat = true; }
                        writer.WriteStartElement(row);
                        foreach (var value in values) writer.WriteElement(SheetExcelCodec.ExportCell(SheetExcelCodec.InheritStyle(sheet, value.Cell), r, value.Column!.Value, styles));
                        writer.WriteEndElement();
                    }
                    writer.WriteEndElement(); writer.WriteEndElement();
                }
                workbook.Workbook.Save();
            }
            // Permission may have been revoked during a long export.
            await Require(id, "edit", ct);
            Log(id, "export", revision.Id, new { });
            await uow.Repository.CompleteAsync(ct);
            return path;
        }
        catch { if (File.Exists(path)) File.Delete(path); throw; }
    }
}
