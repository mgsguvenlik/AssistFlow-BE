using Business.Interfaces.PeriodicReports;
using Business.Models;
using ClosedXML.Excel;
using Core.Enums;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using System.Globalization;
using System.Net;
using System.Text;

namespace Business.Services.PeriodicReports
{
    internal static class ReportExportHelpers
    {
        public static string SafeFileStem(string value)
        {
            var invalid = Path.GetInvalidFileNameChars().ToHashSet();
            var cleaned = new string(value
                .Select(character => invalid.Contains(character) ? '_' : character)
                .ToArray())
                .Trim();

            if (string.IsNullOrWhiteSpace(cleaned))
                cleaned = "report";

            return cleaned.Length <= 120 ? cleaned : cleaned[..120];
        }

        public static string FormatValue(object? value) => value switch
        {
            null => string.Empty,
            DateTime dateTime => dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => value.ToString() ?? string.Empty
        };
    }

    public sealed class ExcelReportExporter : IReportExporter
    {
        public PeriodicReportOutputFormat Format => PeriodicReportOutputFormat.Excel;

        public Task<ReportFile> ExportAsync(string reportName, ReportData data, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Rapor");

            for (var columnIndex = 0; columnIndex < data.Columns.Count; columnIndex++)
            {
                worksheet.Cell(1, columnIndex + 1).Value = data.Columns[columnIndex];
                worksheet.Cell(1, columnIndex + 1).Style.Font.Bold = true;
                worksheet.Cell(1, columnIndex + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E78");
                worksheet.Cell(1, columnIndex + 1).Style.Font.FontColor = XLColor.White;
            }

            for (var rowIndex = 0; rowIndex < data.Rows.Count; rowIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (var columnIndex = 0; columnIndex < data.Columns.Count; columnIndex++)
                {
                    var value = data.Rows[rowIndex].GetValueOrDefault(data.Columns[columnIndex]);
                    var cell = worksheet.Cell(rowIndex + 2, columnIndex + 1);
                    switch (value)
                    {
                        case null: cell.Value = string.Empty; break;
                        case bool boolean: cell.Value = boolean; break;
                        case byte number: cell.Value = number; break;
                        case short number: cell.Value = number; break;
                        case int number: cell.Value = number; break;
                        case long number: cell.Value = number; break;
                        case float number: cell.Value = number; break;
                        case double number: cell.Value = number; break;
                        case decimal number: cell.Value = number; break;
                        case DateTime dateTime: cell.Value = dateTime; break;
                        case DateTimeOffset dateTimeOffset: cell.Value = dateTimeOffset.DateTime; break;
                        default: cell.Value = ReportExportHelpers.FormatValue(value); break;
                    }
                }
            }

            if (data.Columns.Count > 0)
            {
                worksheet.SheetView.FreezeRows(1);
                worksheet.RangeUsed()?.SetAutoFilter();
                worksheet.ColumnsUsed().AdjustToContents(1, Math.Min(data.Rows.Count + 1, 500));
                foreach (var column in worksheet.ColumnsUsed())
                {
                    if (column.Width > 60)
                        column.Width = 60;
                }
            }

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var fileName = $"{ReportExportHelpers.SafeFileStem(reportName)}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
            return Task.FromResult(new ReportFile(
                fileName,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                stream.ToArray()));
        }
    }

    public sealed class CsvReportExporter : IReportExporter
    {
        public PeriodicReportOutputFormat Format => PeriodicReportOutputFormat.Csv;

        public Task<ReportFile> ExportAsync(string reportName, ReportData data, CancellationToken cancellationToken)
        {
            var builder = new StringBuilder();
            builder.AppendLine(string.Join(',', data.Columns.Select(Escape)));

            foreach (var row in data.Rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                builder.AppendLine(string.Join(',', data.Columns.Select(column =>
                    Escape(ReportExportHelpers.FormatValue(row.GetValueOrDefault(column))))));
            }

            var preamble = Encoding.UTF8.GetPreamble();
            var body = Encoding.UTF8.GetBytes(builder.ToString());
            var content = new byte[preamble.Length + body.Length];
            Buffer.BlockCopy(preamble, 0, content, 0, preamble.Length);
            Buffer.BlockCopy(body, 0, content, preamble.Length, body.Length);

            return Task.FromResult(new ReportFile(
                $"{ReportExportHelpers.SafeFileStem(reportName)}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv",
                "text/csv; charset=utf-8",
                content));
        }

        private static string Escape(string value) =>
            $"\"{value.Replace("\"", "\"\"")}\"";
    }

    public sealed class HtmlReportExporter : IReportExporter
    {
        public PeriodicReportOutputFormat Format => PeriodicReportOutputFormat.Html;

        public Task<ReportFile> ExportAsync(string reportName, ReportData data, CancellationToken cancellationToken)
        {
            var builder = new StringBuilder();
            builder.Append("<!doctype html><html><head><meta charset=\"utf-8\"><style>")
                .Append("body{font-family:Arial,sans-serif;font-size:12px}table{border-collapse:collapse;width:100%}")
                .Append("th,td{border:1px solid #ccc;padding:6px;text-align:left}th{background:#1f4e78;color:white}")
                .Append("tr:nth-child(even){background:#f5f7fa}</style></head><body><h2>")
                .Append(WebUtility.HtmlEncode(reportName))
                .Append("</h2><table><thead><tr>");

            foreach (var column in data.Columns)
                builder.Append("<th>").Append(WebUtility.HtmlEncode(column)).Append("</th>");

            builder.Append("</tr></thead><tbody>");
            foreach (var row in data.Rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                builder.Append("<tr>");
                foreach (var column in data.Columns)
                {
                    builder.Append("<td>")
                        .Append(WebUtility.HtmlEncode(ReportExportHelpers.FormatValue(row.GetValueOrDefault(column))))
                        .Append("</td>");
                }
                builder.Append("</tr>");
            }

            builder.Append("</tbody></table></body></html>");
            return Task.FromResult(new ReportFile(
                $"{ReportExportHelpers.SafeFileStem(reportName)}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.html",
                "text/html; charset=utf-8",
                Encoding.UTF8.GetBytes(builder.ToString())));
        }
    }

    public sealed class PdfReportExporter : IReportExporter
    {
        private const int MaxColumnsPerPage = 8;
        public PeriodicReportOutputFormat Format => PeriodicReportOutputFormat.Pdf;

        public Task<ReportFile> ExportAsync(string reportName, ReportData data, CancellationToken cancellationToken)
        {
            using var document = new PdfDocument();
            document.Info.Title = reportName;

            var columnGroups = Math.Max(1, (int)Math.Ceiling(data.Columns.Count / (double)MaxColumnsPerPage));
            for (var groupIndex = 0; groupIndex < columnGroups; groupIndex++)
            {
                var groupColumns = data.Columns
                    .Skip(groupIndex * MaxColumnsPerPage)
                    .Take(MaxColumnsPerPage)
                    .ToList();
                RenderColumnGroup(document, reportName, groupColumns, data.Rows, cancellationToken);
            }

            using var stream = new MemoryStream();
            document.Save(stream, closeStream: false);
            return Task.FromResult(new ReportFile(
                $"{ReportExportHelpers.SafeFileStem(reportName)}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf",
                "application/pdf",
                stream.ToArray()));
        }

        private static void RenderColumnGroup(
            PdfDocument document,
            string reportName,
            IReadOnlyList<string> columns,
            IReadOnlyList<Dictionary<string, object?>> rows,
            CancellationToken cancellationToken)
        {
            const double margin = 24;
            const double titleHeight = 28;
            const double rowHeight = 18;
            var rowIndex = 0;
            var firstPage = true;

            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                var page = document.AddPage();
                page.Orientation = PdfSharpCore.PageOrientation.Landscape;
                using var graphics = XGraphics.FromPdfPage(page);
                var titleFont = new XFont("Arial", 12, XFontStyle.Bold);
                var headerFont = new XFont("Arial", 7, XFontStyle.Bold);
                var cellFont = new XFont("Arial", 7, XFontStyle.Regular);
                var availableWidth = page.Width.Point - margin * 2;
                var columnWidth = columns.Count == 0 ? availableWidth : availableWidth / columns.Count;
                var y = margin;

                graphics.DrawString(reportName, titleFont, XBrushes.Black,
                    new XRect(margin, y, availableWidth, titleHeight), XStringFormats.CenterLeft);
                y += titleHeight;

                for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
                {
                    var rectangle = new XRect(margin + columnIndex * columnWidth, y, columnWidth, rowHeight);
                    graphics.DrawRectangle(new XSolidBrush(XColor.FromArgb(31, 78, 120)), rectangle);
                    graphics.DrawString(Trim(columns[columnIndex], 24), headerFont, XBrushes.White,
                        new XRect(rectangle.X + 3, rectangle.Y, rectangle.Width - 6, rectangle.Height), XStringFormats.CenterLeft);
                }
                y += rowHeight;

                var renderedAny = false;
                while (rowIndex < rows.Count && y + rowHeight <= page.Height.Point - margin)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
                    {
                        var rectangle = new XRect(margin + columnIndex * columnWidth, y, columnWidth, rowHeight);
                        graphics.DrawRectangle(XPens.LightGray, rectangle);
                        var text = ReportExportHelpers.FormatValue(rows[rowIndex].GetValueOrDefault(columns[columnIndex]));
                        graphics.DrawString(Trim(text, 42), cellFont, XBrushes.Black,
                            new XRect(rectangle.X + 3, rectangle.Y, rectangle.Width - 6, rectangle.Height), XStringFormats.CenterLeft);
                    }

                    renderedAny = true;
                    rowIndex++;
                    y += rowHeight;
                }

                if (rows.Count == 0 || (!renderedAny && !firstPage))
                    break;

                firstPage = false;
            }
            while (rowIndex < rows.Count);
        }

        private static string Trim(string value, int maxLength) =>
            value.Length <= maxLength ? value : value[..(maxLength - 1)] + "…";
    }
}
