using System.Text;
using System.Text.Json;
using System.Globalization;

// Read-only, aggregate-only preview. Never prints customer identifiers or payloads.
internal static class CollectionSnapshotProfiler
{
    public static async Task RunAsync(string customerPath, string contractPath, string historyPath)
    {
        Console.WriteLine("Kaynak kesit profili (yalnız kod ve adet; kişisel veri gösterilmez):");
        await CountAsync(customerPath, "Müşteri türü", "Type");
        await CountAsync(contractPath, "Servis tipi", "ServiceTypeID");
        await CountAsync(contractPath, "Sözleşme durumu", "ContractStatusID");
        await CountAsync(contractPath, "Abonelik durumu", "SubscriptionStatusID");
        await CountAsync(contractPath, "Ödeme yöntemi", "PaymentMethodID");
        await CountAsync(contractPath, "Sözleşme ödeme dönemi", "PaymentTypeID");
        await CountAsync(contractPath, "Sözleşme para birimi", "CurrencyID");
        await CountAsync(historyPath, "Tarihçe süreç tipi", "ProcessType");
        await CountAsync(historyPath, "Tarihçe ödeme dönemi", "PaymentTypeID");
        await CountAsync(historyPath, "Tarihçe para birimi", "CurrencyID");
        await CountAttachmentsAsync(contractPath);
    }

    private static async Task CountAsync(string path, string title, string property)
    {
        var counts = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        await ForEachAsync(path, root =>
        {
            var value = SafeCode(Value(root, property), property);
            counts[value] = counts.GetValueOrDefault(value) + 1;
        });
        Console.WriteLine($"{title} ({counts.Count} farklı değer):");
        foreach (var (value, count) in counts.OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            Console.WriteLine($"  {value}: {count}");
    }

    private static async Task CountAttachmentsAsync(string path)
    {
        long complete = 0, incomplete = 0, empty = 0;
        await ForEachAsync(path, root =>
        {
            var hasName = Value(root, "FileAttachmentName") is not null;
            var hasPath = Value(root, "FileAttachmentPath") is not null;
            if (hasName && hasPath) complete++;
            else if (hasName || hasPath) incomplete++;
            else empty++;
        });
        Console.WriteLine($"Dosya metadata: tam={complete}, eksik eş={incomplete}, yok={empty}. Fiziksel dosya varlığı bu profilde doğrulanmaz.");
    }

    private static async Task ForEachAsync(string path, Action<JsonElement> visit)
    {
        await using var stream = File.OpenRead(path);
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true));
        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            using var document = JsonDocument.Parse(line);
            visit(document.RootElement);
        }
    }

    private static string? Value(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var element) || element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        var text = element.ValueKind == JsonValueKind.String ? element.GetString() : element.GetRawText();
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    private static string SafeCode(string? raw, string property)
    {
        if (raw is null) return "(boş)";
        if (property == "Type")
            return raw.ToUpperInvariant() is "N" or "GM" or "A" or "G" ? raw.ToUpperInvariant() : "(beklenmeyen tür)";
        if (property == "ProcessType")
            return raw.ToUpper(CultureInfo.GetCultureInfo("tr-TR")) switch
            {
                "BAŞLANGIÇ" => "Başlangıç",
                "FİYAT ARTIŞI" => "Fiyat Artışı",
                "FİYAT ARTISI" or "FİYAT ARTİSİ" => "Fiyat Artışı (ASCII legacy yazım)",
                "FİYAT İNDİRİMİ" => "Fiyat İndirimi",
                "TURKCELL GPRS ZAM YANSITMASI" => "Turkcell GPRS Zam Yansıtması",
                "ÜCRETSİZ" => "Ücretsiz",
                "HİZMET DONDURMA" => "Hizmet Dondurma",
                _ => "(beklenmeyen süreç)"
            };
        return long.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id >= 0
            ? id.ToString(CultureInfo.InvariantCulture) : "(sayısal olmayan kimlik)";
    }
}
