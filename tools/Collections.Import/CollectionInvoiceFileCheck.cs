using Business.Services.Crm.Collections;
using ClosedXML.Excel;

internal static class CollectionInvoiceFileCheck
{
    public static void Run()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Faturalar");
        string[] headers = ["Cari Kod", "Müşteri Adı", "No", "Tarih", "Tutar", "Ödendi mi", "Açıklama", "Proje Kodu", "Para Birimi"];
        for (var i = 0; i < headers.Length; i++) sheet.Cell(1, i + 1).Value = headers[i];
        sheet.Cell(2, 1).Value = "00120";
        sheet.Cell(2, 2).Value = "K07 doğrulama";
        sheet.Cell(2, 3).Value = "000001";
        sheet.Cell(2, 4).Value = new DateTime(2026, 9, 29);
        sheet.Cell(2, 5).Value = "1.234,56";
        sheet.Cell(2, 6).Value = "Evet";
        sheet.Cell(2, 9).Value = "TL";
        using var output = new MemoryStream();
        byte[] Bytes() { workbook.SaveAs(output); return output.ToArray(); }
        var parsed = CollectionInvoiceFileParser.Parse(Bytes(), "fatura.xlsx", "B");
        var row = parsed.Rows.Single();
        if (row.Errors.Count != 0 || row.Amount != 1234.56m || row.AccountCode != "00120" || row.Number != "000001" || row.Date != new DateOnly(2026, 9, 29) || row.CurrencyCode != "TRY" || row.PaidText != "Evet")
            throw new InvalidOperationException("Geçerli satır veya kod/tarih/tutar koruma kontrolü başarısız.");
        sheet.Cell(2, 5).Value = "1.234";
        if (CollectionInvoiceFileParser.Parse(Bytes(), "fatura.xlsx", "B").Rows[0].Amount != null)
            throw new InvalidOperationException("Belirsiz tutar kabul edildi.");
        sheet.Cell(2, 5).FormulaA1 = "1+2";
        if (!CollectionInvoiceFileParser.Parse(Bytes(), "fatura.xlsx", "B").Rows[0].Errors.Any(x => x.Contains("Formül")))
            throw new InvalidOperationException("Formül reddedilmedi.");
        sheet.Cell(2, 5).Clear(); sheet.Cell(2, 5).Value = 12.5;
        sheet.Row(2).CopyTo(sheet.Row(3));
        if (CollectionInvoiceFileParser.Parse(Bytes(), "fatura.xlsx", "K").Rows.Any(x => x.Errors.Count == 0))
            throw new InvalidOperationException("Tekrarlanan satırlar reddedilmedi.");
        sheet.Cell(1, 1).Value = "Yanlış başlık";
        try { CollectionInvoiceFileParser.Parse(Bytes(), "fatura.xlsx", "B"); throw new Exception("Eksik başlık kabul edildi."); }
        catch (InvalidDataException) { }
        Console.WriteLine("K07 dosya okuma kontrolü başarılı: başlıklar, metin kodlar, tarih/tutar, formül ve tekrar kontrolü. DB/CDN işlemi yapılmadı.");
    }
}
