using System.Globalization;
using System.Text.Json;
using Data.Concrete.EfCore.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Model.Concrete;
using Model.Concrete.Collections;

internal static class CollectionReferenceMapImporter
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static async Task RunAsync(string mapPath, string settingsPath)
    {
        using var mapDocument = JsonDocument.Parse(await File.ReadAllTextAsync(mapPath));
        var root = mapDocument.RootElement;
        var sourceSystem = Required(root, "sourceSystem", 50);
        var snapshotKey = Required(root, "snapshotKey", 200);
        long? actorId = null;
        if (root.TryGetProperty("approvedByUserId", out var approver)
            && approver.ValueKind != JsonValueKind.Null)
        {
            if (!approver.TryGetInt64(out var parsedActorId) || parsedActorId <= 0)
                throw new InvalidOperationException("Onaylayan kullanıcı kimliği geçersiz.");
            actorId = parsedActorId;
        }
        var approvalReference = Required(root, "approvalReference", 500);
        if (!root.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Array
            || entries.GetArrayLength() is < 1 or > 500)
            throw new InvalidOperationException("1-500 arası onaylı referans eşlemesi gereklidir.");

        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath), new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        });
        var connection = new SqlConnectionStringBuilder(settings.RootElement.GetProperty("AppSettings")
            .GetProperty("MSSQLConnectionString").GetString());
        if (connection.DataSource != "192.168.1.8" || connection.InitialCatalog != "AssistFlowTest")
            throw new InvalidOperationException("Referans eşlemesi yalnız AssistFlowTest üzerinde çalışır.");
        var options = new DbContextOptionsBuilder<AppDataContext>().UseSqlServer(connection.ConnectionString).Options;
        await using var db = new AppDataContext(options);
        if (actorId.HasValue && !await db.Users.AsNoTracking().AnyAsync(x => x.Id == actorId.Value && !x.IsDeleted))
            throw new InvalidOperationException("Onaylayan kullanıcı bulunamadı veya silinmiş.");
        var batch = await db.Set<CollectionMigrationBatch>().AsNoTracking().SingleOrDefaultAsync(x =>
            x.SourceSystem == sourceSystem && x.SnapshotKey == snapshotKey)
            ?? throw new InvalidOperationException("Önce kaynak kesiti staging'e alınmalıdır.");
        if (batch.Status is not (CollectionMigrationBatchStatus.Staged or CollectionMigrationBatchStatus.NeedsReview))
            throw new InvalidOperationException("Bu durumdaki kesite eşleme eklenemez.");

        var proposed = new List<(string Kind, string SourceId, long TargetId, string Expected, string Evidence)>();
        var keys = new HashSet<(string, string)>();
        foreach (var entry in entries.EnumerateArray())
        {
            var kind = Required(entry, "kind", 40);
            var sourceId = NormalizeSourceId(Required(entry, "sourceId", 100));
            var expected = Required(entry, "expectedValue", 200);
            var evidence = Required(entry, "evidence", 1000);
            if (!entry.TryGetProperty("targetId", out var target) || !target.TryGetInt64(out var targetId)
                || targetId <= 0 || !keys.Add((kind, sourceId)))
                throw new InvalidOperationException("Eşleme hedefi veya tekrarlanan kaynak anahtarı geçersiz.");
            proposed.Add((kind, sourceId, targetId, expected, evidence));
        }
        var existing = await db.Set<CollectionMigrationReferenceMap>().AsNoTracking()
            .Where(x => x.BatchId == batch.Id).ToListAsync();
        foreach (var entry in proposed)
        {
            var actual = await TargetValueAsync(db, entry.Kind, entry.TargetId);
            if (actual is null || Normalize(actual) != Normalize(entry.Expected))
                throw new InvalidOperationException($"{entry.Kind} kaynağı için hedef kayıt bulunamadı veya beklenen ad/kod ile uyuşmuyor.");
            var prior = existing.SingleOrDefault(x => x.ReferenceKind == entry.Kind
                && NormalizeSourceId(x.SourceId) == entry.SourceId);
            if (prior is null) continue;
            if (prior.Status != CollectionMigrationDecisionStatus.Accepted || TargetId(prior) != entry.TargetId)
                throw new InvalidOperationException($"{entry.Kind} kaynağı daha önce farklı bir kararla eşlenmiş; otomatik değiştirilmez.");
        }
        var added = 0;
        foreach (var entry in proposed)
        {
            if (existing.Any(x => x.ReferenceKind == entry.Kind
                && NormalizeSourceId(x.SourceId) == entry.SourceId)) continue;
            var map = new CollectionMigrationReferenceMap
            {
                BatchId = batch.Id, ReferenceKind = entry.Kind, SourceId = entry.SourceId,
                MatchMethod = "ExplicitApproved", Evidence = $"{approvalReference} | {entry.Evidence}",
                Status = CollectionMigrationDecisionStatus.Accepted,
                DecidedUser = actorId, DecidedDate = DateTimeOffset.UtcNow
            };
            switch (entry.Kind)
            {
                case "ServiceType": map.TargetServiceTypeId = entry.TargetId; break;
                case "CurrencyType": map.TargetCurrencyTypeId = entry.TargetId; break;
                case "CustomerType": map.TargetCustomerTypeId = entry.TargetId; break;
                case "PaymentFrequency": map.TargetPaymentFrequencyId = entry.TargetId; break;
                case "PaymentMethod": map.TargetPaymentMethodId = entry.TargetId; break;
                case "SubscriptionStatus": map.TargetSubscriptionStatusId = entry.TargetId; break;
                case "ContractStatus": map.TargetContractStatusId = entry.TargetId; break;
                default: throw new InvalidOperationException("Bu referans türü açık eşleme komutunda desteklenmiyor.");
            }
            db.Set<CollectionMigrationReferenceMap>().Add(map);
            added++;
        }
        if (added > 0) await db.SaveChangesAsync();
        Console.WriteLine($"{added} yeni onaylı referans eşlemesi kaydedildi; {proposed.Count - added} eşleme değişmeden korundu.");
    }

    private static async Task<string?> TargetValueAsync(AppDataContext db, string kind, long id) => kind switch
    {
        "ServiceType" => await db.Set<ServiceType>().AsNoTracking()
            .Where(x => x.Id == id && !x.IsDeleted).Select(x => x.Name).SingleOrDefaultAsync(),
        "CurrencyType" => await db.Set<CurrencyType>().AsNoTracking()
            .Where(x => x.Id == id).Select(x => x.Code).SingleOrDefaultAsync(),
        "CustomerType" => await db.Set<CustomerType>().AsNoTracking()
            .Where(x => x.Id == id).Select(x => x.Code).SingleOrDefaultAsync(),
        "PaymentFrequency" => await db.Set<CollectionPaymentFrequency>().AsNoTracking()
            .Where(x => x.Id == id).Select(x => x.Code).SingleOrDefaultAsync(),
        "PaymentMethod" => await db.Set<CollectionPaymentMethod>().AsNoTracking()
            .Where(x => x.Id == id).Select(x => x.Code).SingleOrDefaultAsync(),
        "SubscriptionStatus" => await db.Set<CollectionSubscriptionStatus>().AsNoTracking()
            .Where(x => x.Id == id).Select(x => x.Code).SingleOrDefaultAsync(),
        "ContractStatus" => await db.Set<CollectionContractStatus>().AsNoTracking()
            .Where(x => x.Id == id).Select(x => x.Code).SingleOrDefaultAsync(),
        _ => throw new InvalidOperationException("Desteklenmeyen referans türü.")
    };

    private static long? TargetId(CollectionMigrationReferenceMap map) => map.ReferenceKind switch
    {
        "ServiceType" => map.TargetServiceTypeId,
        "CurrencyType" => map.TargetCurrencyTypeId,
        "CustomerType" => map.TargetCustomerTypeId,
        "PaymentFrequency" => map.TargetPaymentFrequencyId,
        "PaymentMethod" => map.TargetPaymentMethodId,
        "SubscriptionStatus" => map.TargetSubscriptionStatusId,
        "ContractStatus" => map.TargetContractStatusId,
        _ => null
    };

    private static string Normalize(string value) => string.Join(' ', value.Trim()
        .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpper(Turkish);

    private static string NormalizeSourceId(string value)
    {
        var trimmed = value.Trim();
        return long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
            ? id.ToString(CultureInfo.InvariantCulture) : trimmed;
    }

    private static string Required(JsonElement root, string property, int maxLength)
    {
        if (!root.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException($"Eşleme {property} alanı eksik.");
        var text = value.GetString();
        return string.IsNullOrWhiteSpace(text) || text.Length > maxLength
            ? throw new InvalidOperationException($"Eşleme {property} alanı geçersiz.") : text;
    }
}
