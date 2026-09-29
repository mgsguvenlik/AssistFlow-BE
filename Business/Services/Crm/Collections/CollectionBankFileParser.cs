using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Model.Dtos.Crm.Collections;

namespace Business.Services.Crm.Collections;

/// <summary>Reads only payment evidence; never persists the original bank workbook or card data.</summary>
public static partial class CollectionBankFileParser
{
    public const int MaximumBytes = 10 * 1024 * 1024;
    public const int MaximumRows = 5000;
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static CollectionBankFilePreview Parse(byte[] bytes, string fileName, string type, DateOnly period)
    {
        if (type is not ("GTS" or "IVR")) throw new InvalidDataException("GTS veya IVR dosya türünü seçin.");
        if (period == default || period.Day != 1 || period.Year == 9999)
            throw new InvalidDataException("Geçerli bir ödeme dönemi seçin (ayın ilk günü).");
        if (!string.Equals(Path.GetExtension(fileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Banka dosyasını Excel'de .xlsx biçiminde kaydedip yükleyin.");
        if (bytes.Length is < 1 or > MaximumBytes) throw new InvalidDataException("Dosya boş olamaz ve 10 MB sınırını aşamaz.");
        string[] headers = type == "GTS"
            ? ["RRN", "Kayıt No", "İşlem Tarihi", "İşlem Tipi", "Tutar", "Kur", "Dönüş Kodu"]
            : ["İşlem Numarası", "Sipariş Numarası", "İşlem Tarihi", "İşlem Tipi", "Sipariş Tutarı", "Döviz cinsi", "İşlem Durumu"];
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
            var last = sheet.LastRowUsed(XLCellsUsedOptions.Contents)!.RowNumber();
            if (last > MaximumRows + 1) throw new InvalidDataException("Bir dosyada en fazla 5.000 veri satırı olabilir.");
            var headerCells = sheet.Row(1).CellsUsed(XLCellsUsedOptions.Contents).ToArray();
            if (headerCells.Any(x => x.HasFormula)) throw new InvalidDataException("Başlık hücrelerinde formül bulunamaz.");
            var groups = headerCells.GroupBy(x => x.GetString().Trim(), StringComparer.Create(Turkish, true)).ToArray();
            if (groups.Any(x => x.Count() > 1)) throw new InvalidDataException("Tekrarlanan kolon başlıklarını düzeltin.");
            var columns = groups.ToDictionary(x => x.Key, x => x.Single().Address.ColumnNumber, StringComparer.Create(Turkish, true));
            var missing = headers.Where(x => !columns.ContainsKey(x)).ToArray();
            if (missing.Length != 0) throw new InvalidDataException("Eksik kolonlar: " + string.Join(", ", missing));
            var rows = new List<CollectionBankFileRow>();
            for (var n = 2; n <= last; n++)
            {
                var cells = headers.Select(h => sheet.Cell(n, columns[h])).ToArray();
                if (cells.All(x => x.IsEmpty())) continue;
                var issues = new List<string>();
                var formula = cells.Any(x => x.HasFormula);
                if (formula) issues.Add("Formüllü satır işlenmez; hücreleri değer olarak yapıştırın.");
                string Text(int i) => cells[i].HasFormula ? "" : cells[i].GetString().Trim();
                var reference = Text(1);
                var transaction = Text(0);
                if (transaction.Length is < 1 or > 100 || reference.Length is < 1 or > 100)
                    issues.Add("İşlem numarası ve sözleşme referansı 1–100 karakter olmalıdır.");
                if (cells[0].DataType == XLDataType.Number || cells[1].DataType == XLDataType.Number)
                    issues.Add("İşlem numarası ve sözleşme referansı metin olmalıdır; baştaki sıfırlar doğrulanamıyor.");
                var date = formula ? null : ReadDate(cells[2]);
                if (date is null) issues.Add("İşlem tarihi geçersiz; Excel tarihi veya gün/ay/yıl saat biçimini kullanın.");
                var amount = formula ? null : ReadAmount(cells[4]);
                if (amount is null || amount <= 0) issues.Add("Tutar pozitif ve en fazla iki ondalıklı olmalıdır.");
                var process = Text(3);
                var currency = Text(5).ToUpperInvariant();
                if (currency is "TL" or "YTL" or "TRL") currency = "TRY";
                if (currency is not ("TRY" or "USD" or "EUR" or "GBP"))
                    issues.Add("Para birimi doğrulanmalı; kur dönüşümü otomatik yapılmaz.");
                var result = Text(6);
                // A numeric zero is not silently treated as the bank's textual success code 00.
                var success = type == "GTS" ? result == "00" : string.Equals(result, "Başarılı", StringComparison.OrdinalIgnoreCase);
                if (result.Length == 0) issues.Add("Banka işlem sonucu boş.");
                if (type == "GTS" && process != "Mail Order")
                    issues.Add("İşlem türü otomatik tahsilata uygun değil; iade/iptal ayrıca incelenmelidir.");
                if (type == "IVR" && process != "Satış")
                    issues.Add("İşlem türü otomatik tahsilata uygun değil; iade/iptal ayrıca incelenmelidir.");
                if (process.Length > 100 || result.Length > 100) issues.Add("İşlem türü veya banka sonucu çok uzun.");
                var status = result.Length > 0 && !success ? "BankFailed" : issues.Count > 0 ? "Review" : "Candidate";
                rows.Add(new(n, transaction[..Math.Min(transaction.Length, 100)], reference[..Math.Min(reference.Length, 100)],
                    date, process[..Math.Min(process.Length, 100)], amount, currency[..Math.Min(currency.Length, 20)],
                    result[..Math.Min(result.Length, 100)], status, issues));
            }
            if (rows.Count == 0) throw new InvalidDataException("Dosyada işlenecek banka satırı bulunamadı.");
            var duplicates = rows.Where(x => x.TransactionNumber.Length > 0).GroupBy(x => x.TransactionNumber, StringComparer.Ordinal)
                .Where(x => x.Count() > 1).SelectMany(x => x.Select(r => r.RowNumber)).ToHashSet();
            rows = rows.Select(x => duplicates.Contains(x.RowNumber)
                ? x with { Status = "Review", Issues = [.. x.Issues, "İşlem numarası dosyada birden fazla kez bulunuyor."] } : x).ToList();
            return new(Convert.ToHexString(SHA256.HashData(bytes)), type, period, rows);
        }
        catch (InvalidDataException) { throw; }
        catch (Exception ex) when (ex is not OutOfMemoryException and not OperationCanceledException)
        { throw new InvalidDataException("Banka dosyası okunamadı; dosyayı .xlsx olarak yeniden kaydedin.", ex); }
    }

    private static DateTime? ReadDate(IXLCell cell)
    {
        if (cell.DataType == XLDataType.DateTime) return cell.GetDateTime();
        if (cell.DataType == XLDataType.Number) return null;
        return DateTime.TryParseExact(cell.GetString().Trim(),
            ["dd/MM/yyyy HH:mm:ss", "d/M/yyyy H:mm:ss", "dd.MM.yyyy HH:mm:ss", "yyyy-MM-dd HH:mm:ss", "dd.MM.yyyy", "yyyy-MM-dd"],
            Turkish, DateTimeStyles.None, out var date) ? date : null;
    }

    private static decimal? ReadAmount(IXLCell cell)
    {
        decimal amount;
        if (cell.DataType == XLDataType.Number)
        { if (!cell.TryGetValue<decimal>(out amount)) return null; }
        else
        {
            var text = cell.GetString().Trim();
            if (TurkishAmount().IsMatch(text))
            { if (!decimal.TryParse(text, NumberStyles.Number, Turkish, out amount)) return null; }
            else if (InvariantAmount().IsMatch(text))
            { if (!decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out amount)) return null; }
            else return null;
        }
        return amount < 10000000000000000m && amount > -10000000000000000m && decimal.Round(amount, 2) == amount ? amount : null;
    }

    [GeneratedRegex(@"^-?(?:\d+|\d{1,3}(?:\.\d{3})+),\d{1,2}$")]
    private static partial Regex TurkishAmount();
    [GeneratedRegex(@"^-?\d+(?:\.\d{1,2})?$")]
    private static partial Regex InvariantAmount();
}
