using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Model.Dtos.Crm.Collections;

namespace Business.Services.Crm.Collections;

/// <summary>Validates legacy invoice workbook content before CDN upload or database mutation.</summary>
public static partial class CollectionInvoiceFileParser
{
    public const int MaximumBytes = 10 * 1024 * 1024;
    public const int MaximumRows = 5000;
    private static readonly string[] Headers = ["Cari Kod", "Müşteri Adı", "No", "Tarih", "Tutar", "Ödendi mi", "Açıklama", "Proje Kodu", "Para Birimi"];
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static CollectionInvoiceFilePreview Parse(byte[] bytes, string fileName, string type)
    {
        if (type is not ("B" or "K")) throw new InvalidDataException("Bireysel veya kurumsal fatura türünü seçin.");
        if (!string.Equals(Path.GetExtension(fileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Fatura dosyası .xlsx olmalıdır. Eski .xls dosyasını Excel'de .xlsx olarak kaydedin.");
        if (bytes.Length is < 1 or > MaximumBytes) throw new InvalidDataException("Dosya boş olamaz ve 10 MB sınırını aşamaz.");
        using var input = new MemoryStream(bytes, writable: false);
        try
        {
            using (var zip = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true))
            {
                if (zip.Entries.Count > 2000 || zip.Entries.Sum(x => x.Length) > 100L * 1024 * 1024 ||
                    zip.GetEntry("xl/workbook.xml") is null || zip.Entries.Any(x => x.FullName.Contains("vbaProject", StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidDataException("Dosyanın Excel içeriği veya açılmış boyutu uygun değil.");
            }
            input.Position = 0;
            using var workbook = new XLWorkbook(input);
            var sheets = workbook.Worksheets.Where(x => x.LastRowUsed(XLCellsUsedOptions.Contents) is not null).ToArray();
            if (sheets.Length != 1) throw new InvalidDataException("Dosyada tek bir dolu çalışma sayfası bulunmalıdır.");
            var sheet = sheets[0];
            var lastRow = sheet.LastRowUsed(XLCellsUsedOptions.Contents)!.RowNumber();
            if (lastRow > MaximumRows + 1) throw new InvalidDataException("Bir dosyada en fazla 5.000 veri satırı olabilir.");
            var headerCells = sheet.Row(1).CellsUsed(XLCellsUsedOptions.Contents).ToArray();
            if (headerCells.Any(x => x.HasFormula)) throw new InvalidDataException("Başlık hücrelerinde formül bulunamaz.");
            var groups = headerCells.GroupBy(x => x.GetString().Trim(), StringComparer.Create(Turkish, true)).ToArray();
            if (groups.Any(x => x.Count() != 1)) throw new InvalidDataException("Dosyada tekrarlanan kolon başlığı bulunuyor.");
            var columns = groups.ToDictionary(x => x.Key, x => x.Single().Address.ColumnNumber, StringComparer.Create(Turkish, true));
            var missing = Headers.Where(x => !columns.ContainsKey(x)).ToArray();
            if (missing.Length != 0) throw new InvalidDataException("Eksik kolonlar: " + string.Join(", ", missing));
            var rows = new List<CollectionInvoiceFileRow>();
            for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
            {
                var cells = Headers.ToDictionary(x => x, x => sheet.Cell(rowNumber, columns[x]));
                if (cells.Values.All(x => x.IsEmpty())) continue;
                var errors = new List<string>();
                var hasFormula = cells.Values.Any(x => x.HasFormula);
                if (hasFormula) errors.Add("Formülleri değer olarak yapıştırın; formüllü satır işlenmez.");
                string Text(string name) => cells[name].HasFormula ? "" : cells[name].GetString().Trim();
                var account = Text("Cari Kod");
                var number = Text("No");
                var customer = Text("Müşteri Adı");
                var project = Text("Proje Kodu");
                var comment = Text("Açıklama");
                var paid = Text("Ödendi mi");
                var currency = Text("Para Birimi").ToUpperInvariant();
                if (currency is "TL" or "YTL" or "TRL") currency = "TRY";
                if (account.Length is < 1 or > 200) errors.Add("Cari Kod 1–200 karakter olmalıdır.");
                if (cells["Cari Kod"].DataType == XLDataType.Number) errors.Add("Cari Kod metin biçiminde olmalıdır; baştaki sıfırlar doğrulanamıyor.");
                if (number.Length is < 1 or > 50) errors.Add("Fatura No 1–50 karakter olmalıdır.");
                if (cells["No"].DataType == XLDataType.Number) errors.Add("Fatura No metin biçiminde olmalıdır; baştaki sıfırlar doğrulanamıyor.");
                if (project.Length > 100) errors.Add("Proje Kodu en fazla 100 karakter olabilir.");
                if (comment.Length > 10000 || customer.Length > 500) errors.Add("Açıklama veya müşteri adı çok uzun.");
                if (currency.Length is < 1 or > 20) errors.Add("Para Birimi boş veya geçersiz.");
                var date = hasFormula ? null : ReadDate(cells["Tarih"]);
                var amount = hasFormula ? null : ReadAmount(cells["Tutar"]);
                if (date is null) errors.Add("Tarih geçersiz. Excel tarihi veya gg.AA.yyyy biçimini kullanın.");
                if (amount is null) errors.Add("Tutar geçersiz. En fazla iki ondalıklı sayısal tutar kullanın.");
                if (paid.Length > 50) errors.Add("Ödendi mi alanı en fazla 50 karakter olabilir.");
                rows.Add(new(rowNumber, account, customer, number, date, amount, currency, paid, comment, project, errors));
            }
            if (rows.Count == 0) throw new InvalidDataException("Dosyada işlenecek fatura satırı bulunamadı.");
            var duplicateRows = rows.Where(x => x.AccountCode.Length > 0 && x.Number.Length > 0)
                .GroupBy(x => (x.AccountCode, x.Number)).Where(g => g.Count() > 1)
                .SelectMany(g => g.Select(x => x.RowNumber)).ToHashSet();
            rows = rows.Select(x => duplicateRows.Contains(x.RowNumber)
                ? x with { Errors = [.. x.Errors, "Aynı cari kod ve fatura numarası dosyada birden fazla kez bulunuyor."] } : x).ToList();
            return new(Convert.ToHexString(SHA256.HashData(bytes)), type, rows);
        }
        catch (InvalidDataException) { throw; }
        catch (Exception ex) when (ex is not OutOfMemoryException and not OperationCanceledException)
        { throw new InvalidDataException("Excel dosyası okunamadı. Dosyayı .xlsx olarak yeniden kaydedip deneyin.", ex); }
    }

    private static DateOnly? ReadDate(IXLCell cell)
    {
        if (cell.DataType == XLDataType.DateTime) return DateOnly.FromDateTime(cell.GetDateTime());
        // Unformatted serials can use the 1900 or 1904 epoch; do not guess dates from raw numbers.
        if (cell.DataType == XLDataType.Number) return null;
        return DateOnly.TryParseExact(cell.GetString().Trim(), ["dd.MM.yyyy", "d.M.yyyy", "yyyy-MM-dd"], Turkish, DateTimeStyles.None, out var date) ? date : null;
    }
    private static decimal? ReadAmount(IXLCell cell)
    {
        decimal amount;
        if (cell.DataType == XLDataType.Number)
        {
            if (!cell.TryGetValue<decimal>(out amount)) return null;
        }
        else
        {
            var text = cell.GetString().Trim();
            if (TurkishAmount().IsMatch(text))
            {
                if (!decimal.TryParse(text, NumberStyles.Number, Turkish, out amount)) return null;
            }
            else if (InvariantAmount().IsMatch(text))
            {
                if (!decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out amount)) return null;
            }
            else return null;
        }
        return Math.Abs(amount) < 10000000000000000m && decimal.Round(amount, 2) == amount ? amount : null;
    }
    [GeneratedRegex(@"^-?(?:\d+|\d{1,3}(?:\.\d{3})+),\d{1,2}$")]
    private static partial Regex TurkishAmount();
    [GeneratedRegex(@"^-?\d+(?:\.\d{1,2})?$")]
    private static partial Regex InvariantAmount();
}
