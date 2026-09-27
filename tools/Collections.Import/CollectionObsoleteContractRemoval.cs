using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Data.Concrete.EfCore.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Model.Concrete.Collections;

/// <summary>Madde 4'te açıkça incelenen eski test aktarımlarını yedekli ve atomik olarak kaldırır.</summary>
internal static class CollectionObsoleteContractRemoval
{
    private const string AuditCode = "CUSTOMER_IMPORTED_SUPERSEDED";
    private const string ApprovedIds = "54,193,249,264,282,406,473,511,529,851,865,898,1273,1369,1756,1785,1862,1871,1893,1954,2095,2177,2207,2330,2485,2530,2560,3593,9615,9617,12061,12125,12169,12223,12238,18775,22863,24960,25069,25221,25336,25346,25375,25392,26486,27573,28619,28739,28745,28874,29036,29046,29054,30114,30131,30144,30145,30188,30232,30238,30242,30278,30280,30353,30354,31394,31438,31439,32481,32506,32507,32643,32644,32676,32677,32844,32845,32882,32883,32891,32892,32893,32894,32948,32949,33196,38487,38488,44096,44187,44200,44286,44288,44327,44328,44602,44670,44714,44800,44815,44826,44966,44967,44991,45007,45083,45084,45195,46157,46158,46160,46185,46187,46272,46273,46322,46323,46344,46345,46380,46651,47657,47658,47822,47823,47824,48017,48018,48240,48241,48286,48399,48400,48592,48758,48759,48764,49263";

    public static async Task RunAsync(long batchId, string settingsPath, string? expectedHash)
    {
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath), new JsonDocumentOptions
        { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        var connection = new SqlConnectionStringBuilder(settings.RootElement.GetProperty("AppSettings").GetProperty("MSSQLConnectionString").GetString());
        if (connection.DataSource != "192.168.1.8" || connection.InitialCatalog != "AssistFlowTest" || batchId != 1)
            throw new InvalidOperationException("Kaldırma yalnız incelenmiş AssistFlowTest kesiti 1 için geçerlidir.");
        await using var db = new AppDataContext(new DbContextOptionsBuilder<AppDataContext>()
            .UseSqlServer(connection.ConnectionString, sql => sql.CommandTimeout(180)).Options);
        await using var tx = expectedHash is null ? null : await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        if (tx is not null)
            await db.Database.ExecuteSqlRawAsync("""
                DECLARE @result int;
                EXEC @result = sys.sp_getapplock @Resource=N'CollectionLegacyContractApply',
                    @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=15000;
                IF @result<0 THROW 51000, N'Aktarım karar kilidi alınamadı.', 1;
                """);
        var batch = await db.Set<CollectionMigrationBatch>().SingleAsync(x => x.Id == batchId);
        Require(batch.SourceSystem == "MGS" && batch.SnapshotKey == "mgs-20260919-131119Z"
            && batch.Status == CollectionMigrationBatchStatus.NeedsReview, "Kesit kimliği/durumu beklenenden farklı.");
        var ids = ApprovedIds.Split(',').ToHashSet(StringComparer.Ordinal);
        var stages = (await db.Set<CollectionMigrationContractStage>().Include(x => x.SourceRow)
            .Where(x => x.SourceRow.BatchId == batchId).OrderBy(x => x.SourceRowId).ToListAsync())
            .Where(x => ids.Contains(CollectionTransferPlan.NormalizeId(x.SourceRow.SourceId))).ToList();
        Require(stages.Count == 138, "Onaylı 138 kaynak sözleşme bulunamadı.");
        var rateStages = (await db.Set<CollectionMigrationRatePeriodStage>().Include(x => x.SourceRow)
            .Where(x => x.SourceRow.BatchId == batchId).OrderBy(x => x.SourceRowId).ToListAsync())
            .Where(x => ids.Contains(CollectionTransferPlan.NormalizeId(x.SourceContractId))).ToList();
        Require(rateStages.Count == 563, "Onaylı 563 tarihçe bulunamadı.");
        var rows = stages.Select(x => x.SourceRow).Concat(rateStages.Select(x => x.SourceRow)).ToList();
        foreach (var row in rows) Require(SHA256.HashData(Encoding.UTF8.GetBytes(row.Payload)).SequenceEqual(row.PayloadHash), "Kaynak hash değişmiş.");
        var rowIds = rows.Select(x => x.Id).ToArray();
        var issues = await db.Set<CollectionMigrationIssue>().Where(x => rowIds.Contains(x.SourceRowId)).OrderBy(x => x.Id).ToListAsync();
        var historyIds = rateStages.Select(x => x.SourceRow.SourceId).ToHashSet();
        var allMaps = await db.Set<CollectionMigrationMap>().OrderBy(x => x.Id).ToListAsync();
        var maps = allMaps.Where(x => x.SourceSystem == "MGS" &&
            (x.EntityCode == "Contract" && ids.Contains(CollectionTransferPlan.NormalizeId(x.SourceId))
            || x.EntityCode == "ContractHistory" && historyIds.Contains(x.SourceId))).ToList();
        if (stages.All(x => x.Status == CollectionMigrationRowStatus.Excluded)
            && rateStages.All(x => x.Status == CollectionMigrationRowStatus.Excluded) && maps.Count == 0
            && stages.All(x => issues.Any(i => i.SourceRowId == x.SourceRowId && i.IssueCode == AuditCode && i.Status == CollectionMigrationIssueStatus.Resolved)))
        {
            Console.WriteLine("Madde 4: 138 eski sözleşme / 563 tarife zaten kaldırılmış ve denetim kaydı mevcut; değişiklik yok.");
            return;
        }
        Require(stages.All(x => x.Status == CollectionMigrationRowStatus.Applied) && rateStages.All(x => x.Status == CollectionMigrationRowStatus.Applied),
            "Kısmi veya beklenmeyen aktarım durumu; kaldırma yapılmadı.");
        var sources = await db.Set<CollectionMigrationSourceRow>().AsNoTracking()
            .Where(x => x.BatchId == batchId && x.EntityCode == "Customer").OrderBy(x => x.Id).ToListAsync();
        var replacements = CollectionCustomerDuplicateReviewer.LatestNameReplacements(sources);
        Require(stages.All(x => replacements.ContainsKey(CollectionTransferPlan.NormalizeId(x.SourceCustomerId))), "En yeni müşteri kararı doğrulanamadı.");
        var contractMaps = maps.Where(x => x.EntityCode == "Contract").ToDictionary(x => CollectionTransferPlan.NormalizeId(x.SourceId));
        var rateMaps = maps.Where(x => x.EntityCode == "ContractHistory").ToDictionary(x => x.SourceId);
        Require(contractMaps.Count == 138 && rateMaps.Count == 563 && maps.All(x => x.FirstBatchId == batchId && x.LastBatchId == batchId && x.TargetPaymentId == null),
            "Aktarım eşlemeleri beklenen kapsamda değil.");
        Require(stages.All(x => contractMaps.TryGetValue(CollectionTransferPlan.NormalizeId(x.SourceRow.SourceId), out var m)
                && m.TargetContractId.HasValue && m.TargetRatePeriodId == null && m.AppliedPayloadHash.SequenceEqual(x.SourceRow.PayloadHash))
            && rateStages.All(x => rateMaps.TryGetValue(x.SourceRow.SourceId, out var m)
                && m.TargetRatePeriodId.HasValue && m.TargetContractId == null && m.AppliedPayloadHash.SequenceEqual(x.SourceRow.PayloadHash)), "Eşleme/kaynak hash uyumsuz.");
        var targetIds = contractMaps.Values.Select(x => x.TargetContractId!.Value).ToArray();
        var contracts = await db.Set<CollectionContract>().Where(x => targetIds.Contains(x.Id)).OrderBy(x => x.Id).ToListAsync();
        var rates = await db.Set<CollectionContractRatePeriod>().Where(x => targetIds.Contains(x.ContractId)).OrderBy(x => x.Id).ToListAsync();
        var rateIds = rates.Select(x => x.Id).ToHashSet();
        Require(contracts.Count == 138 && rates.Count == 563 && rateIds.SetEquals(rateMaps.Values.Select(x => x.TargetRatePeriodId!.Value)), "Hedef sözleşme/tarife kümesi değişmiş.");
        Require(allMaps.Where(x => x.TargetContractId.HasValue && targetIds.Contains(x.TargetContractId.Value)
                || x.TargetRatePeriodId.HasValue && rateIds.Contains(x.TargetRatePeriodId.Value)).All(x => maps.Contains(x)), "Kapsam dışında eşleme var.");
        Require(contracts.All(x => !x.IsDeleted && x.CreatedUser == 0 && x.UpdatedDate == null && x.UpdatedUser == null && x.CreationRequestId == null)
            && rates.All(x => !x.IsDeleted && x.CreatedUser == 0 && x.UpdatedDate == null && x.UpdatedUser == null), "Hedef kayıt kullanıcı tarafından değiştirilmiş; kaldırma durduruldu.");
        var byContractId = contracts.ToDictionary(x => x.Id);
        var byRateId = rates.ToDictionary(x => x.Id);
        Require(stages.All(x => byContractId[contractMaps[CollectionTransferPlan.NormalizeId(x.SourceRow.SourceId)].TargetContractId!.Value].CustomerId == x.TargetCustomerId),
            "Hedef müşteri eşlemesi değişmiş.");
        Require(rateStages.All(x =>
        {
            var target = byRateId[rateMaps[x.SourceRow.SourceId].TargetRatePeriodId!.Value];
            return target.ContractId == contractMaps[CollectionTransferPlan.NormalizeId(x.SourceContractId)].TargetContractId
                && target.EffectiveFrom == x.EffectiveFrom && target.EffectiveToExclusive == x.EffectiveToExclusive
                && target.Amount == x.Amount && target.CurrencyTypeId == x.TargetCurrencyTypeId
                && target.PaymentFrequencyId == x.TargetPaymentFrequencyId && target.BillingBehavior == x.BillingBehavior;
        }), "Hedef tarife değerleri değişmiş.");
        Require(!await db.Set<CollectionPayment>().AnyAsync(x => targetIds.Contains(x.ContractId))
            && !await db.Set<CollectionContractAttachment>().AnyAsync(x => targetIds.Contains(x.ContractId))
            && !await db.Set<CollectionContractPeriodFollowUp>().AnyAsync(x => targetIds.Contains(x.ContractId)), "Ödeme, dosya veya takip bağımlılığı var; kaldırma yapılmadı.");
        object[] Scalars<T>(IEnumerable<T> entities) where T : class => entities.Select(x => (object)db.Entry(x).Properties
            .OrderBy(p => p.Metadata.Name).ToDictionary(p => p.Metadata.Name, p => p.CurrentValue)).ToArray();
        var backup = JsonSerializer.SerializeToUtf8Bytes(new
        {
            Policy = "item-four-138-obsolete-payments-stay-in-MGS-files-item-eight-v1", Database = "AssistFlowTest",
            Batch = Scalars(new[] { batch }), Contracts = Scalars(contracts), Rates = Scalars(rates), Maps = Scalars(maps),
            ContractStages = Scalars(stages), RateStages = Scalars(rateStages), Issues = Scalars(issues), SourceRows = Scalars(rows),
            SourceCustomers = sources.Select(x => new { x.Id, x.SourceId, x.PayloadHash, x.Payload }),
            Replacements = stages.Select(x => new { x.SourceRow.SourceId, x.SourceCustomerId,
                Winner = replacements[CollectionTransferPlan.NormalizeId(x.SourceCustomerId)] })
        }, new JsonSerializerOptions { WriteIndented = true });
        var hash = Convert.ToHexString(SHA256.HashData(backup));
        Console.WriteLine($"Kaldırma planı: sözleşme={contracts.Count}; tarife={rates.Count}; eşleme={maps.Count}; ödeme/dosya/takip=0. SHA-256: {hash}");
        if (expectedHash is null) { Console.WriteLine("DRY-RUN; veri değiştirilmedi."); return; }
        Require(hash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase), "Kaldırma planı değişmiş; yeni önizleme gerekiyor.");
        var output = Path.GetFullPath(Path.Combine("tools", "Collections.Import", "snapshots", "item-four-removal-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff")));
        Directory.CreateDirectory(output);
        var backupPath = Path.Combine(output, "backup.json");
        await using (var file = new FileStream(backupPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.WriteThrough))
        {
            await file.WriteAsync(backup);
            file.Flush(flushToDisk: true);
        }
        Require(Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(backupPath))) == hash, "Yedek doğrulanamadı; silme yapılmadı.");
        Console.WriteLine($"Doğrulanmış yerel yedek: {backupPath}");
        db.Set<CollectionMigrationMap>().RemoveRange(maps);
        await db.SaveChangesAsync();
        db.Set<CollectionContractRatePeriod>().RemoveRange(rates);
        await db.SaveChangesAsync();
        db.Set<CollectionContract>().RemoveRange(contracts);
        var now = DateTimeOffset.UtcNow;
        foreach (var stage in stages)
        {
            stage.Status = CollectionMigrationRowStatus.Excluded;
            var note = $"26.09.2026 madde 4: yalnız en yeni abonelik korunur. Eski müşteri={stage.SourceCustomerId}; korunan={replacements[CollectionTransferPlan.NormalizeId(stage.SourceCustomerId)]}; kaldırılan test sözleşmesi={contractMaps[CollectionTransferPlan.NormalizeId(stage.SourceRow.SourceId)].TargetContractId}. Eski ödemeler yalnız MGS'de kalır; dosyalar madde 8. Yedek SHA-256={hash}.";
            db.Set<CollectionMigrationIssue>().Add(new() { SourceRowId = stage.SourceRowId, IssueCode = AuditCode,
                Severity = CollectionMigrationIssueSeverity.Warning, Status = CollectionMigrationIssueStatus.Resolved,
                Details = note, ResolutionNote = note, RuleVersion = batch.RuleVersion, CreatedDate = now, ResolvedDate = now });
        }
        foreach (var stage in rateStages) stage.Status = CollectionMigrationRowStatus.Excluded;
        foreach (var issue in issues.Where(x => x.Status == CollectionMigrationIssueStatus.Open))
        {
            issue.Status = CollectionMigrationIssueStatus.Ignored;
            issue.ResolvedDate = now;
            issue.ResolutionNote = $"26.09.2026 madde 4: eski abonelik dışlandı; ödemeler MGS'de, dosyalar madde 8. Yedek SHA-256={hash}.";
        }
        await db.SaveChangesAsync();
        await tx!.CommitAsync();
        Console.WriteLine("COMMIT: 138 eski test sözleşmesi ve 563 tarife yedeklenerek kaldırıldı; staging dışlandı. MGS, dbo müşteri ve dosyalara yazılmadı.");
        await CollectionMigrationReconciler.RunAsync(batch.SourceSystem, batch.SnapshotKey, batch.ManifestHash, settingsPath);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
