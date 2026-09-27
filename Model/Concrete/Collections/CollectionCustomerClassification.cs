using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace Model.Concrete.Collections;

public enum CollectionCustomerClass { Unknown, Excluded, Individual, Group }

/// <summary>Onaylı kod kararı; API, sorgular ve aktarım aynı sürümü kullanır.</summary>
public static class CollectionCustomerClassification
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly string[] Individual;
    private static readonly string[] Groups;
    private static readonly string[] Excluded;
    private static readonly string[] ExcludedPrefixes;
    public static IReadOnlyList<string> IndividualCodes => Array.AsReadOnly(Individual);
    public static IReadOnlyList<string> GroupCodes => Array.AsReadOnly(Groups);
    public static readonly string[] IndividualTypeCodes = ["N", "BRYSL01"];
    public static readonly string[] GroupTypeCodes = ["GM", "GRP01"];
    public static readonly string[] GroupOperationalTypeCodes = ["GM", "GRP01", "G"];
    public static string PolicyHash { get; }
    public const string OutsideScopeMessage = "Müşteri tahsilat kapsamında değil veya müşteri tipi grup koduyla uyuşmuyor. Yeni tahsilat işlemi yapılamaz; geçmiş kayıtlar korunur.";

    static CollectionCustomerClassification()
    {
        using var stream = typeof(CollectionCustomerClassification).Assembly
            .GetManifestResourceStream("CollectionCustomerClassification.json")
            ?? throw new InvalidOperationException("Tahsilat müşteri sınıflandırma kararı bulunamadı.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        var bytes = memory.ToArray();
        PolicyHash = Convert.ToHexString(SHA256.HashData(bytes));
        using var doc = JsonDocument.Parse(bytes);
        string[] Read(string name) => doc.RootElement.GetProperty(name).EnumerateArray()
            .Select(x => Normalize(x.GetString())).Distinct(StringComparer.Ordinal).ToArray();
        Individual = Read("individualCodes");
        Groups = Read("explicitGroupCodes").Concat(Read("legacyVerifiedGroupCodes")).Distinct().ToArray();
        Excluded = Read("excludedCodes");
        ExcludedPrefixes = Read("excludedPrefixes");
        if (Individual.Intersect(Groups).Any() || Individual.Concat(Groups).Any(x =>
                Excluded.Contains(x) || ExcludedPrefixes.Any(p => x.StartsWith(p, StringComparison.Ordinal))))
            throw new InvalidOperationException("Tahsilat sınıflandırma kararında çakışan kod var.");
    }

    public static string Normalize(string? value) => value?.Trim().ToUpper(Turkish) ?? "";

    public static CollectionCustomerClass Classify(string? groupCode)
    {
        var code = Normalize(groupCode);
        if (Excluded.Contains(code) || ExcludedPrefixes.Any(p => code.StartsWith(p, StringComparison.Ordinal)))
            return CollectionCustomerClass.Excluded;
        if (Individual.Contains(code)) return CollectionCustomerClass.Individual;
        return Groups.Contains(code) ? CollectionCustomerClass.Group : CollectionCustomerClass.Unknown;
    }

    public static string? Issue(string? groupCode, string? typeCode) => Classify(groupCode) switch
    {
        CollectionCustomerClass.Excluded => "CUSTOMER_COLLECTION_EXCLUDED",
        CollectionCustomerClass.Unknown => "CUSTOMER_COLLECTION_UNKNOWN",
        CollectionCustomerClass.Individual when IndividualTypeCodes.Contains(Normalize(typeCode)) => null,
        CollectionCustomerClass.Group when GroupOperationalTypeCodes.Contains(Normalize(typeCode)) => null,
        _ => "CUSTOMER_TYPE_MISMATCH"
    };
}
