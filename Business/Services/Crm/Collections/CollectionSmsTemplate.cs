using System.Text.RegularExpressions;

namespace Business.Services.Crm.Collections;

/// <summary>Allows only named parameters; stores no secrets and never evaluates template code.</summary>
public static class CollectionSmsTemplate
{
    public static readonly IReadOnlySet<string> Parameters = new HashSet<string>(StringComparer.Ordinal)
    {
        "MusteriAdi", "AboneNo", "HizmetAdi", "EskiTutar", "YeniTutar", "ZamOrani",
        "ParaBirimi", "OdemeDonemi", "GecerlilikTarihi"
    };

    public static string Render(string template, IReadOnlyDictionary<string, string> values)
    {
        if (string.IsNullOrWhiteSpace(template) || template.Length > 2000)
            throw new InvalidOperationException("Zam SMS şablonu boş olamaz ve 2000 karakteri geçemez.");
        if (!template.Contains("{GecerlilikTarihi}", StringComparison.Ordinal))
            throw new InvalidOperationException("Zam SMS şablonu {GecerlilikTarihi} parametresini içermelidir.");
        var unmatched = Regex.Replace(template, @"\{([A-Za-z]+)\}", "");
        if (unmatched.Contains('{') || unmatched.Contains('}'))
            throw new InvalidOperationException("Zam SMS şablonunda geçersiz parametre kullanılmış.");
        var result = Regex.Replace(template, @"\{([A-Za-z]+)\}", match =>
        {
            var key = match.Groups[1].Value;
            if (!Parameters.Contains(key) || !values.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"Zam SMS parametresi tanımsız veya eksik: {key}.");
            return value;
        });
        if (result.Length > 2000)
            throw new InvalidOperationException("Oluşturulan zam SMS metni 2000 karakteri geçemez.");
        return result;
    }
}
