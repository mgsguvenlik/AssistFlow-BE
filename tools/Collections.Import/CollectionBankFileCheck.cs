using Business.Services.Crm.Collections;
using ClosedXML.Excel;
using System.Text.Json;

internal static class CollectionBankFileCheck
{
    public static void Run()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Banka");
        string[] headers = ["RRN", "Kayıt No", "İşlem Tarihi", "İşlem Tipi", "Tutar", "Kur", "Dönüş Kodu", "1. Kredi Kartı No"];
        for (var i = 0; i < headers.Length; i++) sheet.Cell(1, i + 1).Value = headers[i];
        sheet.Cell(2, 1).Value = "000001";
        sheet.Cell(2, 2).Value = "00120";
        sheet.Cell(2, 3).Value = new DateTime(2026, 9, 29, 10, 30, 0);
        sheet.Cell(2, 4).Value = "Mail Order";
        sheet.Cell(2, 5).Value = "1.234,56";
        sheet.Cell(2, 6).Value = "TL";
        sheet.Cell(2, 7).Value = "00";
        sheet.Cell(2, 8).Value = "KART-ALANI-DISARI-CIKMAMALI";
        using var output = new MemoryStream();
        Model.Dtos.Crm.Collections.CollectionBankFilePreview Parse(string type = "GTS")
        { workbook.SaveAs(output); return CollectionBankFileParser.Parse(output.ToArray(), "banka.xlsx", type, new(2026, 9, 1)); }
        var parsed = Parse();
        var row = parsed.Rows.Single();
        if (row.Status != "Candidate" || row.Amount != 1234.56m || row.ContractReference != "00120" || row.CurrencyCode != "TRY" ||
            JsonSerializer.Serialize(parsed).Contains("KART-ALANI-DISARI-CIKMAMALI"))
            throw new InvalidOperationException("Banka dosyası alan/mahremiyet kontrolü başarısız.");
        sheet.Cell(2, 7).Value = "05";
        if (Parse().Rows[0].Status != "BankFailed") throw new InvalidOperationException("Başarısız banka sonucu ayrılmadı.");
        sheet.Cell(2, 7).Value = "00";
        sheet.Cell(2, 4).Value = "Serbest İade";
        if (Parse().Rows[0].Status != "Review") throw new InvalidOperationException("İade tahsilat olarak kabul edildi.");
        sheet.Cell(2, 4).Value = "Mail Order";
        sheet.Cell(2, 5).Value = "1.234";
        if (Parse().Rows[0].Amount is not null) throw new InvalidOperationException("Belirsiz tutar kabul edildi.");
        sheet.Cell(2, 5).FormulaA1 = "1+2";
        if (Parse().Rows[0].Status != "Review") throw new InvalidOperationException("Formül kabul edildi.");
        sheet.Cell(2, 5).Clear(); sheet.Cell(2, 5).Value = 1234.56;
        sheet.Row(2).CopyTo(sheet.Row(3));
        if (Parse().Rows.Any(x => x.Status != "Review")) throw new InvalidOperationException("Tekrarlanan banka işlemi kabul edildi.");
        sheet.Row(3).Clear();
        string[] ivr = ["İşlem Numarası", "Sipariş Numarası", "İşlem Tarihi", "İşlem Tipi", "Sipariş Tutarı", "Döviz cinsi", "İşlem Durumu"];
        for (var i = 0; i < ivr.Length; i++) sheet.Cell(1, i + 1).Value = ivr[i];
        sheet.Cell(2, 7).Value = "Başarılı";
        if (Parse("IVR").Rows[0].Status != "Review") throw new InvalidOperationException("Doğrulanmamış IVR türü kabul edildi.");
        sheet.Cell(2, 4).Value = "Satış";
        if (Parse("IVR").Rows[0].Status != "Candidate") throw new InvalidOperationException("IVR Satış türü kabul edilmedi.");
        Console.WriteLine("K08 dosya kontrolü başarılı: GTS/IVR, banka sonucu, iade, tutar, formül, tekrar ve kart alanı dışlama. DB/CDN yazılmadı.");
    }
}
