using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

internal static class SheetExcelFixture
{
    public static MemoryStream Create()
    {
        var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook, true))
        {
            var book = document.AddWorkbookPart();
            book.Workbook = new Workbook();
            book.AddNewPart<WorkbookStylesPart>().Stylesheet = new Stylesheet(
                new Fonts(new Font(), new Font(new Bold(), new Color { Indexed = 16 }), new Font(new Italic())) { Count = 3 },
                new Fills(new Fill(new PatternFill { PatternType = PatternValues.None }),
                    new Fill(new PatternFill { PatternType = PatternValues.Gray125 })) { Count = 2 },
                new Borders(new Border()) { Count = 1 },
                new CellStyleFormats(new CellFormat()) { Count = 1 },
                new CellFormats(new CellFormat(), new CellFormat { FontId = 1, ApplyFont = true },
                    new CellFormat { FontId = 2, ApplyFont = true }) { Count = 3 });
            var part = book.AddNewPart<WorksheetPart>();
            part.Worksheet = new Worksheet(new SheetDimension { Reference = "A1:D4" },
                new Columns(new Column { Min = 2, Max = 2, Style = 1 }),
                new SheetData(new Row(new Cell { CellReference = "B1", DataType = CellValues.Number, CellValue = new CellValue("42") }) { RowIndex = 1 },
                    new Row { RowIndex = 2, StyleIndex = 2, CustomFormat = true, Height = 30, CustomHeight = true }));
            book.Workbook.Append(new Sheets(new Sheet { Id = book.GetIdOfPart(part), SheetId = 1, Name = "Inherited styles" }));
        }
        stream.Position = 0;
        return stream;
    }
}
