using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Data.Concrete.EfCore.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Model.Concrete.Collections;

// Only immutable, pre-exported NDJSON files are accepted. The source database is never opened here.
var isApply = args.Length == 4 && args[0] is "apply" or "exclude-yok" or "include-unknown" or "exclude-unidentified";
if (!isApply && (args.Length is not (2 or 3) || args[0] is not ("inspect" or "profile" or "stage" or "validate" or "summary" or "prepare-references" or "plan" or "reconcile" or "classify" or "customer-exceptions" or "rate-exceptions" or "attachment-inventory" or "decision-exceptions" or "include-unknown" or "exclude-unidentified")
    || (args[0] is not ("inspect" or "profile") && args.Length != 3)))
{
    if (args.Length == 3 && args[0] == "map")
    {
        await CollectionReferenceMapImporter.RunAsync(args[1], args[2]);
        return;
    }
    throw new InvalidOperationException("Kullanım: inspect/profile <kesit klasörü>, stage/validate/summary/prepare-references/plan/classify/customer-exceptions/rate-exceptions/attachment-inventory/decision-exceptions/include-unknown/exclude-unidentified <kesit klasörü> <Development JSON yolu>, apply/exclude-yok/include-unknown/exclude-unidentified <kesit klasörü> <Development JSON yolu> <plan SHA-256> veya map <eşleme JSON yolu> <Development JSON yolu>.");
}

var directory = Path.GetFullPath(args[1]);
if (!Directory.Exists(directory)) throw new InvalidOperationException("Kesit klasörü bulunamadı.");
var manifestPath = Path.Combine(directory, "manifest.json");
if (!File.Exists(manifestPath)) throw new InvalidOperationException("Kesit manifest dosyası bulunamadı.");
using var manifestDocument = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
var manifest = manifestDocument.RootElement;
var sourceSystem = Required(manifest, "sourceSystem", 50);
var snapshotKey = Required(manifest, "snapshotKey", 200);
var ruleVersion = Required(manifest, "ruleVersion", 50);
var normalizationVersion = Required(manifest, "normalizationVersion", 50);
var customers = await InspectFileAsync(Path.Combine(directory, "customers.ndjson"), "Customer", "CustomerID");
var contracts = await InspectFileAsync(Path.Combine(directory, "contracts.ndjson"), "Contract", "ContractID");
var histories = await InspectFileAsync(Path.Combine(directory, "contract-history.ndjson"), "ContractHistory", "ContractHistoryID");
if (customers.Count == 0 || contracts.Count == 0 || histories.Count == 0)
    throw new InvalidOperationException("Müşteri, sözleşme ve tarihçe kesit dosyaları boş olamaz.");
var manifestValue = JsonSerializer.Serialize(new object[] { "collection-snapshot-v1", sourceSystem,
    snapshotKey, ruleVersion, normalizationVersion, customers.Count, Convert.ToHexString(customers.Hash),
    contracts.Count, Convert.ToHexString(contracts.Hash), histories.Count, Convert.ToHexString(histories.Hash) });
var manifestHash = SHA256.HashData(Encoding.UTF8.GetBytes(manifestValue));
Console.WriteLine($"Kesit doğrulandı: {customers.Count} müşteri, {contracts.Count} sözleşme, {histories.Count} tarihçe; SHA-256 manifest {Convert.ToHexString(manifestHash)}.");
if (args[0] == "inspect") return;
if (args[0] == "profile")
{
    await CollectionSnapshotProfiler.RunAsync(customers.Path, contracts.Path, histories.Path);
    return;
}
if (args[0] == "validate")
{
    await CollectionStageValidator.RunAsync(sourceSystem, snapshotKey, manifestHash,
        customers.Count, contracts.Count, histories.Count, args[2]);
    return;
}
if (args[0] == "summary")
{
    await CollectionStageSummary.RunAsync(sourceSystem, snapshotKey, args[2]);
    return;
}
if (args[0] == "prepare-references")
{
    await CollectionSharedReferencePreparer.RunAsync(sourceSystem, snapshotKey, args[2]);
    return;
}
if (args[0] == "plan")
{
    await CollectionMigrationPlanner.RunAsync(sourceSystem, snapshotKey, manifestHash, args[2]);
    return;
}
if (args[0] == "apply")
{
    await CollectionMigrationApplier.RunAsync(sourceSystem, snapshotKey, manifestHash, args[2], args[3]);
    return;
}
if (args[0] == "reconcile")
{
    await CollectionMigrationReconciler.RunAsync(sourceSystem, snapshotKey, manifestHash, args[2]);
    return;
}
if (args[0] == "classify")
{
    await CollectionMigrationExceptionClassifier.RunAsync(sourceSystem, snapshotKey, manifestHash, args[2], null);
    return;
}
if (args[0] == "exclude-yok")
{
    await CollectionMigrationExceptionClassifier.RunAsync(sourceSystem, snapshotKey, manifestHash, args[2], args[3]);
    return;
}
if (args[0] == "include-unknown")
{
    await CollectionUnknownContractIncluder.RunAsync(sourceSystem, snapshotKey, manifestHash, args[2],
        args.Length == 4 ? args[3] : null);
    return;
}
if (args[0] == "exclude-unidentified")
{
    await CollectionUnidentifiedCustomerExcluder.RunAsync(sourceSystem, snapshotKey, manifestHash, args[2],
        args.Length == 4 ? args[3] : null);
    return;
}
if (args[0] == "customer-exceptions")
{
    await CollectionCustomerExceptionReport.RunAsync(sourceSystem, snapshotKey, manifestHash, args[2], directory);
    return;
}
if (args[0] == "rate-exceptions")
{
    await CollectionRateExceptionReport.RunAsync(sourceSystem, snapshotKey, manifestHash, args[2], directory);
    return;
}
if (args[0] == "attachment-inventory")
{
    await CollectionAttachmentInventory.RunAsync(sourceSystem, snapshotKey, manifestHash, args[2], directory);
    return;
}
if (args[0] == "decision-exceptions")
{
    await CollectionDecisionExceptionReport.RunAsync(sourceSystem, snapshotKey, manifestHash, args[2], directory);
    return;
}

using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(args[2]), new JsonDocumentOptions
{
    AllowTrailingCommas = true,
    CommentHandling = JsonCommentHandling.Skip
});
var connection = new SqlConnectionStringBuilder(settings.RootElement.GetProperty("AppSettings")
    .GetProperty("MSSQLConnectionString").GetString());
if (connection.DataSource != "192.168.1.8" || connection.InitialCatalog != "AssistFlowTest")
    throw new InvalidOperationException("Bu ilk staging komutu yalnız AssistFlowTest üzerinde çalışır.");
var options = new DbContextOptionsBuilder<AppDataContext>()
    .UseSqlServer(connection.ConnectionString, sql => sql.EnableRetryOnFailure()).Options;
await using var db = new AppDataContext(options);
if (db.Model.FindEntityType(typeof(CollectionMigrationBatch)) is null)
    throw new InvalidOperationException("Aktarım staging modeli etkin değil.");

var batch = await db.Set<CollectionMigrationBatch>().SingleOrDefaultAsync(x =>
    x.SourceSystem == sourceSystem && x.SnapshotKey == snapshotKey);
if (batch is not null && (!batch.ManifestHash.SequenceEqual(manifestHash)
    || batch.RuleVersion != ruleVersion || batch.NormalizationVersion != normalizationVersion))
    throw new InvalidOperationException("Aynı kesit anahtarı farklı içerik veya kuralla zaten kayıtlı; yeni kesit anahtarı gerekir.");
if (batch is not null && batch.Status != CollectionMigrationBatchStatus.Staged)
    throw new InvalidOperationException("İşlenmiş kesit yeniden staging'e alınamaz.");
if (batch is null)
{
    batch = new CollectionMigrationBatch
    {
        SourceSystem = sourceSystem, SnapshotKey = snapshotKey, ManifestHash = manifestHash,
        RuleVersion = ruleVersion, NormalizationVersion = normalizationVersion,
        Status = CollectionMigrationBatchStatus.Staged, CreatedDate = DateTimeOffset.UtcNow,
        CreatedUser = 0 // Offline importer; not a CRM user action.
    };
    db.Set<CollectionMigrationBatch>().Add(batch);
    await db.SaveChangesAsync();
}

var added = 0;
foreach (var input in new[] { customers, contracts, histories })
{
    await using (var hashStream = File.OpenRead(input.Path))
    {
        var currentHash = await SHA256.HashDataAsync(hashStream);
        if (!currentHash.SequenceEqual(input.Hash))
            throw new InvalidOperationException($"{input.EntityCode} dosyası incelemeden sonra değişmiş; staging durduruldu.");
    }
    var existing = await db.Set<CollectionMigrationSourceRow>().AsNoTracking()
        .Where(x => x.BatchId == batch.Id && x.EntityCode == input.EntityCode)
        .Select(x => new { x.SourceId, x.PayloadHash }).ToDictionaryAsync(x => x.SourceId, x => x.PayloadHash);
    await using var stream = File.OpenRead(input.Path);
    using var reader = new StreamReader(stream, new UTF8Encoding(false, true));
    string? line;
    while ((line = await reader.ReadLineAsync()) is not null)
    {
        if (string.IsNullOrWhiteSpace(line)) continue;
        using var document = JsonDocument.Parse(line);
        var sourceId = RequiredIdentity(document.RootElement, input.IdProperty);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(line));
        if (existing.TryGetValue(sourceId, out var previous))
        {
            if (!previous.SequenceEqual(hash))
                throw new InvalidOperationException($"Önceden alınmış {input.EntityCode} satırı değişmiş; mevcut ham satır ezilmedi.");
            continue;
        }
        var parentId = input.EntityCode == "ContractHistory"
            ? OptionalIdentity(document.RootElement, "ContractID") : null;
        var sourceRow = new CollectionMigrationSourceRow
        {
            BatchId = batch.Id, EntityCode = input.EntityCode, SourceId = sourceId,
            SourceParentId = parentId, Payload = line, PayloadHash = hash, StagedDate = DateTimeOffset.UtcNow
        };
        db.Set<CollectionMigrationSourceRow>().Add(sourceRow);
        if (input.EntityCode == "Contract")
        {
            db.Set<CollectionMigrationContractStage>().Add(new CollectionMigrationContractStage
            {
                SourceRow = sourceRow,
                SourceCustomerId = OptionalIdentity(document.RootElement, "CustomerID"),
                SourceServiceTypeId = OptionalIdentity(document.RootElement, "ServiceTypeID"),
                SourceContractStatusId = OptionalIdentity(document.RootElement, "ContractStatusID"),
                SourceSubscriptionStatusId = OptionalIdentity(document.RootElement, "SubscriptionStatusID"),
                SourcePaymentMethodId = OptionalIdentity(document.RootElement, "PaymentMethodID"),
                AttachmentName = OptionalText(document.RootElement, "FileAttachmentName", 255),
                AttachmentPath = OptionalText(document.RootElement, "FileAttachmentPath", 1000),
                Status = CollectionMigrationRowStatus.Pending
            });
        }
        else if (input.EntityCode == "ContractHistory")
        {
            db.Set<CollectionMigrationRatePeriodStage>().Add(new CollectionMigrationRatePeriodStage
            {
                SourceRow = sourceRow, SourceContractId = parentId,
                SourceCustomerId = OptionalIdentity(document.RootElement, "CustomerID"),
                SourceCurrencyId = OptionalIdentity(document.RootElement, "CurrencyID"),
                SourcePaymentTypeId = OptionalIdentity(document.RootElement, "PaymentTypeID"),
                Status = CollectionMigrationRowStatus.Pending
            });
        }
        added++;
        if (added % 500 == 0) await FlushAsync();
    }
    await FlushAsync();
}
var stagedContracts = await db.Set<CollectionMigrationSourceRow>().AsNoTracking()
    .CountAsync(x => x.BatchId == batch.Id && x.EntityCode == "Contract");
var stagedCustomers = await db.Set<CollectionMigrationSourceRow>().AsNoTracking()
    .CountAsync(x => x.BatchId == batch.Id && x.EntityCode == "Customer");
var stagedHistories = await db.Set<CollectionMigrationSourceRow>().AsNoTracking()
    .CountAsync(x => x.BatchId == batch.Id && x.EntityCode == "ContractHistory");
if (stagedCustomers != customers.Count || stagedContracts != contracts.Count || stagedHistories != histories.Count)
    throw new InvalidOperationException("Staging satır sayısı kesit manifestiyle uyuşmuyor; aktarım başlamadı.");
Console.WriteLine($"Staging tamamlandı: {added} yeni ham satır. Yükleme/finansal kayıt oluşturulmadı.");

async Task FlushAsync()
{
    if (!db.ChangeTracker.HasChanges()) return;
    await db.SaveChangesAsync();
    db.ChangeTracker.Clear();
}

static string Required(JsonElement value, string property, int maxLength)
{
    if (!value.TryGetProperty(property, out var item) || item.ValueKind != JsonValueKind.String)
        throw new InvalidOperationException($"Manifest {property} alanı eksik.");
    var text = item.GetString();
    if (string.IsNullOrWhiteSpace(text) || text.Length > maxLength)
        throw new InvalidOperationException($"Manifest {property} alanı geçersiz.");
    return text;
}

static string RequiredIdentity(JsonElement value, string property)
{
    var identity = OptionalIdentity(value, property);
    return string.IsNullOrWhiteSpace(identity)
        ? throw new InvalidOperationException($"Kaynak {property} kimliği eksik.")
        : identity;
}

static string? OptionalIdentity(JsonElement value, string property)
{
    if (!value.TryGetProperty(property, out var item) || item.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        return null;
    var text = item.ValueKind switch
    {
        JsonValueKind.String => item.GetString(),
        JsonValueKind.Number => item.GetRawText(),
        _ => throw new InvalidOperationException($"Kaynak {property} kimlik türü geçersiz.")
    };
    if (text is null || text.Length > 100) throw new InvalidOperationException($"Kaynak {property} kimliği çok uzun.");
    return text;
}

static string? OptionalText(JsonElement value, string property, int maxLength)
{
    if (!value.TryGetProperty(property, out var item) || item.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        return null;
    if (item.ValueKind != JsonValueKind.String)
        throw new InvalidOperationException($"Kaynak {property} alanının metin olması gerekir.");
    var text = item.GetString();
    return text?.Length <= maxLength ? text : null; // Ham değer MigrationSourceRow.Payload içinde korunur.
}

static async Task<InspectedFile> InspectFileAsync(string path, string entityCode, string idProperty)
{
    if (!File.Exists(path)) throw new InvalidOperationException($"{entityCode} kesit dosyası bulunamadı.");
    await using var stream = File.OpenRead(path);
    var fileHash = await SHA256.HashDataAsync(stream);
    stream.Position = 0;
    using var reader = new StreamReader(stream, new UTF8Encoding(false, true));
    var ids = new HashSet<string>(StringComparer.Ordinal);
    string? line;
    var count = 0;
    while ((line = await reader.ReadLineAsync()) is not null)
    {
        if (string.IsNullOrWhiteSpace(line)) continue;
        using var document = JsonDocument.Parse(line);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException($"{entityCode} satırı JSON nesnesi değil.");
        var id = RequiredIdentity(document.RootElement, idProperty);
        if (!ids.Add(id)) throw new InvalidOperationException($"{entityCode} kesitinde tekrar eden kaynak kimliği var.");
        count++;
    }
    return new(path, entityCode, idProperty, count, fileHash);
}

internal sealed record InspectedFile(string Path, string EntityCode, string IdProperty, int Count, byte[] Hash);
