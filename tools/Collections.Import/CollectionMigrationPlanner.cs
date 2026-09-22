using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Data.Concrete.EfCore.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Model.Concrete.Collections;

internal static class CollectionMigrationPlanner
{
    public static async Task RunAsync(string sourceSystem, string snapshotKey, byte[] expectedManifestHash,
        string settingsPath)
    {
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath), new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        });
        var connection = new SqlConnectionStringBuilder(settings.RootElement.GetProperty("AppSettings")
            .GetProperty("MSSQLConnectionString").GetString());
        if (connection.DataSource != "192.168.1.8" || connection.InitialCatalog != "AssistFlowTest")
            throw new InvalidOperationException("Aktarım planı yalnız AssistFlowTest üzerinde çalışır.");
        var options = new DbContextOptionsBuilder<AppDataContext>().UseSqlServer(connection.ConnectionString, sql =>
        {
            sql.EnableRetryOnFailure();
            sql.CommandTimeout(180);
        }).Options;
        await using var db = new AppDataContext(options);
        var batch = await db.Set<CollectionMigrationBatch>().AsNoTracking().SingleOrDefaultAsync(x =>
            x.SourceSystem == sourceSystem && x.SnapshotKey == snapshotKey)
            ?? throw new InvalidOperationException("Kesit staging kaydı bulunamadı.");
        if (!batch.ManifestHash.SequenceEqual(expectedManifestHash))
            throw new InvalidOperationException("Staging manifest hash'i kesitle uyuşmuyor.");
        if (batch.Status != CollectionMigrationBatchStatus.NeedsReview)
            throw new InvalidOperationException("Aktarım planı yalnız doğrulanmış ve inceleme bekleyen kesitten üretilebilir.");

        var contracts = await db.Set<CollectionMigrationContractStage>().AsNoTracking()
            .Where(x => x.SourceRow.BatchId == batch.Id && x.Status == CollectionMigrationRowStatus.Pending)
            .Select(x => new ContractPlanRow(x.SourceRow.SourceId, x.SourceRow.PayloadHash,
                x.TargetCustomerId, x.TargetServiceTypeId, x.StartingMonth, x.StartingYear,
                x.AttachmentName != null && x.AttachmentPath != null)).ToListAsync();
        var contractIds = contracts.Select(x => NormalizeId(x.SourceId)).ToHashSet(StringComparer.Ordinal);
        var rates = await db.Set<CollectionMigrationRatePeriodStage>().AsNoTracking()
            .Where(x => x.SourceRow.BatchId == batch.Id && x.Status == CollectionMigrationRowStatus.Pending)
            .Select(x => new RatePlanRow(x.SourceContractId, x.Amount, x.TargetCurrencyTypeId,
                x.TargetPaymentFrequencyId, x.BillingBehavior)).ToListAsync();
        var rateGroups = rates.Where(x => x.SourceContractId is not null)
            .GroupBy(x => NormalizeId(x.SourceContractId!), StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.Ordinal);

        var existingContractMapIds = await db.Set<CollectionMigrationMap>().AsNoTracking()
            .Where(x => x.SourceSystem == sourceSystem && x.EntityCode == "Contract")
            .Select(x => x.SourceId).ToListAsync();
        var existingContractMaps = existingContractMapIds.Select(NormalizeId).ToHashSet(StringComparer.Ordinal);
        var eligible = contracts.Where(x => !existingContractMaps.Contains(NormalizeId(x.SourceId))
            && x.TargetCustomerId.HasValue && x.TargetServiceTypeId.HasValue
            && x.StartingMonth.HasValue && x.StartingYear.HasValue
            && rateGroups.TryGetValue(NormalizeId(x.SourceId), out var periods) && periods.Count > 0
            && periods.All(p => p.Amount.HasValue && p.TargetCurrencyTypeId.HasValue
                && p.TargetPaymentFrequencyId.HasValue && p.BillingBehavior.HasValue)).ToList();
        var eligibleIds = eligible.Select(x => NormalizeId(x.SourceId)).ToHashSet(StringComparer.Ordinal);
        var eligibleRates = rates.Where(x => x.SourceContractId is not null
            && eligibleIds.Contains(NormalizeId(x.SourceContractId))).ToList();

        var planMaterial = string.Join('\n', eligible.OrderBy(x => x.SourceId, StringComparer.Ordinal)
            .Select(x => $"{x.SourceId}:{Convert.ToHexString(x.PayloadHash)}"));
        var planHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(planMaterial)));
        Console.WriteLine("DRY-RUN; hiçbir hedef sözleşme, tarife veya ödeme kaydı oluşturulmadı.");
        Console.WriteLine($"Plan SHA-256: {planHash}");
        Console.WriteLine($"Aday sözleşme: {contracts.Count}; tam küme: {eligible.Count}; mevcut map nedeniyle atlanan: {contracts.Count(x => existingContractMaps.Contains(NormalizeId(x.SourceId)))}.");
        Console.WriteLine($"Tam küme tarihçe: {eligibleRates.Count}; müşteri: {eligible.Select(x => x.TargetCustomerId).Distinct().Count()}; servis tipi: {eligible.Select(x => x.TargetServiceTypeId).Distinct().Count()}; dosya metadata: {eligible.Count(x => x.HasAttachment)}.");
        Console.WriteLine("Tam kümeye girememe nedenleri (nedenler aynı sözleşmede birleşebilir):");
        Console.WriteLine($"  Hedef müşteri/servis eksik: {contracts.Count(x => !x.TargetCustomerId.HasValue || !x.TargetServiceTypeId.HasValue)}");
        Console.WriteLine($"  Başlangıç ay/yıl eksik: {contracts.Count(x => !x.StartingMonth.HasValue || !x.StartingYear.HasValue)}");
        Console.WriteLine($"  Pending tarihçe kümesi yok: {contracts.Count(x => !rateGroups.ContainsKey(NormalizeId(x.SourceId)))}");
        Console.WriteLine($"  Tarihçe hedef/tutar eksik: {contracts.Count(x => rateGroups.TryGetValue(NormalizeId(x.SourceId), out var periods) && periods.Any(p => !p.Amount.HasValue || !p.TargetCurrencyTypeId.HasValue || !p.TargetPaymentFrequencyId.HasValue || !p.BillingBehavior.HasValue))}");
        Console.WriteLine($"  Pending tarihçelerin aday sözleşmeye bağlı satırı: {rates.Count(x => x.SourceContractId is not null && contractIds.Contains(NormalizeId(x.SourceContractId)))}");
        Console.WriteLine("Tarife mutabakatı (para birimi | davranış | satır | tutar toplamı):");
        foreach (var group in eligibleRates.GroupBy(x => new { x.TargetCurrencyTypeId, x.BillingBehavior })
                     .OrderBy(x => x.Key.TargetCurrencyTypeId).ThenBy(x => x.Key.BillingBehavior))
            Console.WriteLine($"  {group.Key.TargetCurrencyTypeId}|{group.Key.BillingBehavior}|{group.Count()}|{group.Sum(x => x.Amount):0.00}");
        Console.WriteLine($"Pending olup tam kümeye giremeyen sözleşme: {contracts.Count - eligible.Count}. Bunlar yüklenmez; staging durumu değiştirilmedi.");
    }

    private sealed record ContractPlanRow(string SourceId, byte[] PayloadHash, long? TargetCustomerId,
        long? TargetServiceTypeId, short? StartingMonth, short? StartingYear, bool HasAttachment);
    private sealed record RatePlanRow(string? SourceContractId, decimal? Amount, long? TargetCurrencyTypeId,
        long? TargetPaymentFrequencyId, CollectionBillingBehavior? BillingBehavior);

    private static string NormalizeId(string value)
    {
        var trimmed = value.Trim();
        return long.TryParse(trimmed, out var id) ? id.ToString(System.Globalization.CultureInfo.InvariantCulture) : trimmed;
    }
}
