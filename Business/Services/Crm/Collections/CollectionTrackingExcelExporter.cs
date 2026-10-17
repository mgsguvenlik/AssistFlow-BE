using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Model.Dtos.Crm.Collections;

namespace Business.Services.Crm.Collections;

public static class CollectionTrackingExcelExporter
{
    public static async Task<Stream> CreateAsync(IAsyncEnumerable<CollectionTrackingItem> rows, CancellationToken ct)
    {
        // Seekable temporary stream for ZIP packaging; disposed/deleted by MVC after download.
        var stream = new FileStream(Path.Combine(Path.GetTempPath(), $"collection-export-{Guid.NewGuid():N}.xlsx"),
            FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 65536,
            FileOptions.DeleteOnClose | FileOptions.SequentialScan);
        try
        {
            using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
            {
                var workbook = document.AddWorkbookPart();
                var sheets = new DocumentFormat.OpenXml.Spreadsheet.Sheets();
                workbook.Workbook = new Workbook(sheets);
                var styles = workbook.AddNewPart<WorkbookStylesPart>();
                styles.Stylesheet = new Stylesheet(
                    new Fonts(new Font(), new Font(new Bold()),
                        new Font(new Bold(), new Color { Rgb = "FF2563EB" }),
                        new Font(new Bold(), new Color { Rgb = "FF047857" }),
                        new Font(new Bold(), new Color { Rgb = "FFEA580C" })),
                    new Fills(new Fill(new PatternFill { PatternType = PatternValues.None }),
                        new Fill(new PatternFill { PatternType = PatternValues.Gray125 })),
                    new Borders(new Border()),
                    new CellStyleFormats(new CellFormat()),
                    new CellFormats(new CellFormat(), new CellFormat { FontId = 1, ApplyFont = true },
                        new CellFormat { NumberFormatId = 4, ApplyNumberFormat = true },
                        new CellFormat { FontId = 2, ApplyFont = true, NumberFormatId = 4, ApplyNumberFormat = true },
                        new CellFormat { FontId = 3, ApplyFont = true, NumberFormatId = 4, ApplyNumberFormat = true },
                        new CellFormat { FontId = 4, ApplyFont = true, NumberFormatId = 4, ApplyNumberFormat = true }));
                styles.Stylesheet.Save();
                OpenXmlWriter? writer = null;
                uint rowIndex = 0, sheetId = 0;
                var totals = new Dictionary<long, (string Code, decimal Debt, decimal Paid, decimal Remaining)>();
                try
                {
                    StartSheet();
                    await foreach (var row in rows.WithCancellation(ct))
                    {
                        ct.ThrowIfCancellationRequested();
                        if (rowIndex == 1048576) { FinishSheet(); StartSheet(); }
                        writer!.WriteStartElement(new Row { RowIndex = ++rowIndex });
                        foreach (var value in new[] { row.CustomerGroupName, row.SubscriberCode, row.CustomerName,
                            row.ServiceTypeName, row.Period.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                            row.DueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), row.CurrencyCode })
                            writer.WriteElement(TextCell(value));
                        foreach (var value in new[] { row.AccruedAmount, row.PaymentAmount, row.RemainingAmount })
                            writer.WriteElement(new Cell { DataType = CellValues.Number, StyleIndex = 2,
                                CellValue = new CellValue(value.ToString(CultureInfo.InvariantCulture)) });
                        writer.WriteElement(TextCell(row.IsGroup ? $"{row.ContractCount} sözleşme"
                            : row.HasAccrual ? "Borç dönemi" : "Yalnız ödeme"));
                        writer.WriteEndElement();
                        var previous = totals.GetValueOrDefault(row.CurrencyTypeId);
                        totals[row.CurrencyTypeId] = (row.CurrencyCode,
                            previous.Debt + row.AccruedAmount, previous.Paid + row.PaymentAmount,
                            previous.Remaining + row.RemainingAmount);
                    }
                    var dataEndRow = rowIndex;
                    if (totals.Count > 0)
                    {
                        // Summary covers every exported sheet, not only the final sheet/page.
                        if ((long)rowIndex + totals.Count + 4 > 1048576)
                        {
                            FinishSheet(); StartSheet(); dataEndRow = 1;
                        }
                        rowIndex++;
                        WriteSummaryRow(TextCell("Filtrelenen tüm kayıtların toplamı", 1));
                        WriteSummaryRow(TextCell("Seçilen dönem ve filtrelerdeki tüm kayıtlar · Para birimleri ayrı hesaplanır."));
                        var currencyHeader = TextCell("Para birimi", 1);
                        currencyHeader.CellReference = $"G{rowIndex + 1}";
                        WriteSummaryRow(currencyHeader, TextCell("Dönem borcu", 1),
                            TextCell("Tahsil edilen", 1), TextCell("Net kalan", 1));
                        foreach (var total in totals.Values.OrderBy(x => x.Code))
                        {
                            var currencyCell = TextCell(total.Code, 1);
                            currencyCell.CellReference = $"G{rowIndex + 1}";
                            WriteSummaryRow(currencyCell, AmountCell(total.Debt, 3),
                                AmountCell(total.Paid, 4), AmountCell(total.Remaining, 5));
                        }
                    }
                    FinishSheet(dataEndRow);
                }
                finally { writer?.Dispose(); }
                workbook.Workbook.Save();

                void StartSheet()
                {
                    rowIndex = 1;
                    var part = workbook.AddNewPart<WorksheetPart>();
                    sheets.Append(new Sheet { Id = workbook.GetIdOfPart(part), SheetId = ++sheetId,
                        Name = sheetId == 1 ? "Tahsilat Takibi" : $"Tahsilat Takibi {sheetId}" });
                    writer = OpenXmlWriter.Create(part);
                    writer.WriteStartElement(new Worksheet());
                    writer.WriteElement(new SheetViews(new SheetView(new Pane {
                        VerticalSplit = 1, TopLeftCell = "A2", ActivePane = PaneValues.BottomLeft,
                        State = PaneStateValues.Frozen }) { WorkbookViewId = 0 }));
                    writer.WriteStartElement(new Columns());
                    var widths = new double[] { 24, 20, 40, 28, 14, 16, 14, 20, 20, 20, 22 };
                    for (var i = 0; i < widths.Length; i++)
                        writer.WriteElement(new Column { Min = (uint)i + 1, Max = (uint)i + 1,
                            Width = widths[i], CustomWidth = true });
                    writer.WriteEndElement();
                    writer.WriteStartElement(new SheetData());
                    writer.WriteStartElement(new Row { RowIndex = 1 });
                    foreach (var title in new[] { "Grup", "Abone No", "Müşteri", "Servis Tipi", "Dönem",
                        "Vade", "Para Birimi", "Borç", "Ödeme", "Kalan", "Kayıt Türü" })
                        writer.WriteElement(TextCell(title, 1));
                    writer.WriteEndElement();
                }

                void WriteSummaryRow(params Cell[] cells)
                {
                    ct.ThrowIfCancellationRequested();
                    writer!.WriteStartElement(new Row { RowIndex = ++rowIndex });
                    var firstColumn = cells[0].CellReference?.Value?.StartsWith('G') == true ? 6 : 0;
                    for (var i = 0; i < cells.Length; i++)
                    {
                        cells[i].CellReference = $"{(char)('A' + firstColumn + i)}{rowIndex}";
                        writer.WriteElement(cells[i]);
                    }
                    writer.WriteEndElement();
                }

                void FinishSheet(uint? filterEndRow = null)
                {
                    writer!.WriteEndElement();
                    writer.WriteElement(new AutoFilter { Reference = $"A1:K{filterEndRow ?? rowIndex}" });
                    writer.WriteEndElement();
                    writer.Dispose();
                    writer = null;
                }
            }
            stream.Position = 0;
            return stream;
        }
        catch
        {
            await stream.DisposeAsync();
            throw;
        }
    }

    private static Cell AmountCell(decimal value, uint style) => new()
    {
        DataType = CellValues.Number, StyleIndex = style,
        CellValue = new CellValue(value.ToString(CultureInfo.InvariantCulture))
    };

    private static Cell TextCell(string? value, uint style = 0)
    {
        // Inline text preserves leading zeros and never executes customer text as a formula.
        var text = System.Text.RegularExpressions.Regex.Replace(value ?? "", "[\u0000-\u0008\u000B\u000C\u000E-\u001F]", "");
        if (text.Length > 32767) text = text[..32767];
        return new Cell { DataType = CellValues.InlineString, StyleIndex = style,
            InlineString = new InlineString(new Text(text) { Space = SpaceProcessingModeValues.Preserve }) };
    }
}
