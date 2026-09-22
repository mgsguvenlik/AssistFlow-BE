using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Data.Concrete.EfCore.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Model.Concrete.Collections;

internal static class CollectionMigrationApplier
{
    public static async Task RunAsync(string sourceSystem, string snapshotKey, byte[] expectedManifestHash,
        string settingsPath, string expectedPlanHash)
    {
        if (expectedPlanHash.Length != 64 || !expectedPlanHash.All(Uri.IsHexDigit))
            throw new InvalidOperationException("Geçerli dry-run plan SHA-256 değeri gereklidir.");
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath), new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        });
        var connection = new SqlConnectionStringBuilder(settings.RootElement.GetProperty("AppSettings")
            .GetProperty("MSSQLConnectionString").GetString());
        if (connection.DataSource != "192.168.1.8" || connection.InitialCatalog != "AssistFlowTest")
            throw new InvalidOperationException("Gerçek aktarım yalnız AssistFlowTest üzerinde çalışır.");
        var options = new DbContextOptionsBuilder<AppDataContext>().UseSqlServer(connection.ConnectionString,
            sql => sql.CommandTimeout(300)).Options;
        await using var db = new AppDataContext(options);
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        await db.Database.ExecuteSqlRawAsync("""
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock @Resource=N'CollectionLegacyContractApply',
                @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=15000;
            IF @result < 0 THROW 51000, N'Legacy sözleşme aktarım kilidi alınamadı.', 1;
            """);
        var batch = await db.Set<CollectionMigrationBatch>().SingleOrDefaultAsync(x =>
            x.SourceSystem == sourceSystem && x.SnapshotKey == snapshotKey)
            ?? throw new InvalidOperationException("Kesit staging kaydı bulunamadı.");
        if (!batch.ManifestHash.SequenceEqual(expectedManifestHash) || batch.Status != CollectionMigrationBatchStatus.NeedsReview)
            throw new InvalidOperationException("Kesit doğrulanmış plan durumunda değil veya manifest değişmiş.");

        var existingMaps = await db.Set<CollectionMigrationMap>().Where(x => x.SourceSystem == sourceSystem).ToListAsync();
        var contractMapIds = existingMaps.Where(x => x.EntityCode == "Contract")
            .Select(x => NormalizeId(x.SourceId)).ToHashSet(StringComparer.Ordinal);
        var contracts = await db.Set<CollectionMigrationContractStage>()
            .Include(x => x.SourceRow)
            .Where(x => x.SourceRow.BatchId == batch.Id && x.Status == CollectionMigrationRowStatus.Pending)
            .ToListAsync();
        var rates = await db.Set<CollectionMigrationRatePeriodStage>()
            .Include(x => x.SourceRow)
            .Where(x => x.SourceRow.BatchId == batch.Id && x.Status == CollectionMigrationRowStatus.Pending)
            .ToListAsync();
        var rateGroups = rates.Where(x => x.SourceContractId is not null)
            .GroupBy(x => NormalizeId(x.SourceContractId!)).ToDictionary(x => x.Key, x => x.ToList(), StringComparer.Ordinal);
        var eligible = contracts.Where(x => !contractMapIds.Contains(NormalizeId(x.SourceRow.SourceId))
            && x.TargetCustomerId.HasValue && x.TargetServiceTypeId.HasValue
            && x.StartingMonth.HasValue && x.StartingYear.HasValue
            && rateGroups.TryGetValue(NormalizeId(x.SourceRow.SourceId), out var periods) && periods.Count > 0
            && periods.All(p => p.Amount.HasValue && p.TargetCurrencyTypeId.HasValue
                && p.TargetPaymentFrequencyId.HasValue && p.BillingBehavior.HasValue)).ToList();
        var planMaterial = string.Join('\n', eligible.OrderBy(x => x.SourceRow.SourceId, StringComparer.Ordinal)
            .Select(x => $"{x.SourceRow.SourceId}:{Convert.ToHexString(x.SourceRow.PayloadHash)}"));
        var actualPlanHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(planMaterial)));
        if (!actualPlanHash.Equals(expectedPlanHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Dry-run planı değişmiş; aktarım durduruldu. Güncel hash: {actualPlanHash}");

        var refs = await db.Set<CollectionMigrationReferenceMap>().AsNoTracking()
            .Where(x => x.BatchId == batch.Id && x.Status == CollectionMigrationDecisionStatus.Accepted).ToListAsync();
        var existsContractStatusId = await db.Set<CollectionContractStatus>().AsNoTracking()
            .Where(x => x.Code == "EXISTS" && x.IsActive).Select(x => (long?)x.Id).SingleOrDefaultAsync()
            ?? throw new InvalidOperationException("Aktif VAR sözleşme durumu bulunamadı.");
        long? Map(string kind, string? sourceId) => string.IsNullOrWhiteSpace(sourceId)
            ? kind == "ContractStatus" ? existsContractStatusId : null
            : refs.Where(x => x.ReferenceKind == kind && NormalizeId(x.SourceId) == NormalizeId(sourceId))
                .Select(x => kind switch
                {
                    "ContractStatus" => x.TargetContractStatusId,
                    "SubscriptionStatus" => x.TargetSubscriptionStatusId,
                    "PaymentMethod" => x.TargetPaymentMethodId,
                    _ => null
                }).SingleOrDefault();

        batch.Status = CollectionMigrationBatchStatus.Applying;
        await db.SaveChangesAsync();
        var now = DateTimeOffset.UtcNow;
        var contractEntries = new List<(CollectionMigrationContractStage Stage, CollectionContract Entity)>();
        foreach (var stage in eligible.OrderBy(x => long.Parse(NormalizeId(x.SourceRow.SourceId), CultureInfo.InvariantCulture)))
        {
            using var payload = JsonDocument.Parse(stage.SourceRow.Payload);
            var root = payload.RootElement;
            var contract = new CollectionContract
            {
                CustomerId = stage.TargetCustomerId!.Value,
                ServiceTypeId = stage.TargetServiceTypeId!.Value,
                StartDate = new DateOnly(stage.StartingYear!.Value, stage.StartingMonth!.Value, 1),
                EndDate = stage.EndDate,
                GtsNo = Limited(Text(root, "GtsNo"), 50),
                IvrNo = Limited(Text(root, "IvrNo"), 50),
                ContractStatusId = Map("ContractStatus", Text(root, "ContractStatusID")),
                SubscriptionStatusId = Map("SubscriptionStatus", Text(root, "SubscriptionStatusID")),
                PaymentMethodId = Map("PaymentMethod", Text(root, "PaymentMethodID")),
                CreatedDate = ParseDate(Text(root, "CreatedOn")) ?? now,
                CreatedUser = 0,
                IsDeleted = false
            };
            contractEntries.Add((stage, contract));
        }
        db.AddRange(contractEntries.Select(x => x.Entity));
        await db.SaveChangesAsync();

        var rateEntries = new List<(CollectionMigrationRatePeriodStage Stage, CollectionContractRatePeriod Entity)>();
        foreach (var (stage, contract) in contractEntries)
        {
            db.Add(NewMap(sourceSystem, "Contract", stage.SourceRow, batch.Id, now, targetContractId: contract.Id));
            stage.Status = CollectionMigrationRowStatus.Applied;
            foreach (var rateStage in rateGroups[NormalizeId(stage.SourceRow.SourceId)].OrderBy(x => x.EffectiveFrom))
            {
                using var ratePayload = JsonDocument.Parse(rateStage.SourceRow.Payload);
                var rateRoot = ratePayload.RootElement;
                var rate = new CollectionContractRatePeriod
                {
                    ContractId = contract.Id,
                    EffectiveFrom = rateStage.EffectiveFrom!.Value,
                    EffectiveToExclusive = rateStage.EffectiveToExclusive,
                    BillingAnchor = rateStage.EffectiveFrom.Value,
                    OriginalAnchorDay = null,
                    PaymentFrequencyId = rateStage.TargetPaymentFrequencyId!.Value,
                    Amount = rateStage.Amount,
                    CurrencyTypeId = rateStage.TargetCurrencyTypeId,
                    BillingBehavior = rateStage.BillingBehavior!.Value,
                    ChangeReason = Limited(Text(rateRoot, "Description")) ?? Limited(Text(rateRoot, "ProcessType")) ?? "Legacy aktarım",
                    CreatedDate = ParseDate(Text(rateRoot, "CreatedOn")) ?? now,
                    CreatedUser = 0,
                    IsDeleted = false
                };
                rateEntries.Add((rateStage, rate));
            }
        }
        db.AddRange(rateEntries.Select(x => x.Entity));
        await db.SaveChangesAsync();
        foreach (var (stage, rate) in rateEntries)
        {
            db.Add(NewMap(sourceSystem, "ContractHistory", stage.SourceRow, batch.Id, now, targetRatePeriodId: rate.Id));
            stage.Status = CollectionMigrationRowStatus.Applied;
        }
        batch.Status = CollectionMigrationBatchStatus.NeedsReview; // Blocked rows remain for explicit review.
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        Console.WriteLine($"Aktarım tamamlandı: {contractEntries.Count} sözleşme, {rateEntries.Count} tarife dönemi. Ödeme ve dosya oluşturulmadı.");
    }

    private static CollectionMigrationMap NewMap(string sourceSystem, string entityCode,
        CollectionMigrationSourceRow source, long batchId, DateTimeOffset now,
        long? targetContractId = null, long? targetRatePeriodId = null) => new()
    {
        SourceSystem = sourceSystem, EntityCode = entityCode, SourceId = NormalizeId(source.SourceId),
        TargetContractId = targetContractId, TargetRatePeriodId = targetRatePeriodId,
        AppliedPayloadHash = source.PayloadHash, FirstBatchId = batchId, LastBatchId = batchId,
        CreatedDate = now, LastSeenDate = now
    };

    private static string NormalizeId(string value)
    {
        var trimmed = value.Trim();
        return long.TryParse(trimmed, out var id) ? id.ToString(CultureInfo.InvariantCulture) : trimmed;
    }

    private static string? Text(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var item) || item.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        return item.ValueKind == JsonValueKind.String ? item.GetString()?.Trim() : item.GetRawText();
    }

    private static string? Limited(string? value, int max = 100) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Length <= max ? value : value[..max];

    private static DateTimeOffset? ParseDate(string? value) => DateTimeOffset.TryParse(value,
        CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var date) ? date : null;
}
