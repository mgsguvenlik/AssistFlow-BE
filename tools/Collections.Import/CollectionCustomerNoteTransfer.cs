using System.Security.Cryptography;
using System.Text.Json;
using Business.Services.Crm.Collections;
using Data.Concrete.EfCore.Collections;
using Data.Concrete.EfCore.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Model.Concrete.Collections;
using Model.Dtos.Crm.Collections;

internal static class CollectionCustomerNoteTransfer
{
    private const string Migration = "20260927180000_AddCollectionCustomerNotes";
    public static async Task RunAsync(string settingsPath, string outputPath, string? mode)
    {
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath),
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var connection = new SqlConnectionStringBuilder(settings.RootElement.GetProperty("AppSettings").GetProperty("MSSQLConnectionString").GetString());
        if (connection.DataSource != "192.168.1.8" || connection.InitialCatalog != "AssistFlowTest")
            throw new InvalidOperationException("Not aktarımı yalnız AssistFlowTest üzerinde çalışır.");
        await using var db = new AppDataContext(new DbContextOptionsBuilder<AppDataContext>()
            .UseSqlServer(connection.ConnectionString, o => o.CommandTimeout(120)).Options);
        if (db.Database.HasPendingModelChanges()) throw new InvalidOperationException("Model ve migration snapshot farklı.");
        if (mode == "--install")
        {
            var pending = (await db.Database.GetPendingMigrationsAsync()).ToArray();
            if (pending.Length > 0 && (pending.Length != 1 || pending[0] != Migration))
                throw new InvalidOperationException("Bekleyen migration listesi yalnız müşteri notu migrationını içermelidir.");
            if (pending.Length == 1) await db.Database.MigrateAsync();
            Console.WriteLine("collection.CustomerNote şeması hazır.");
            return;
        }
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        // Only retained contracts and explicitly recorded parent mappings establish note ownership.
        // No name/subscriber guess and no remapping of discarded older subscriptions.
        var candidates = await (from m in db.Set<CollectionMigrationMap>().AsNoTracking()
                                join s in db.Set<CollectionMigrationSourceRow>().AsNoTracking()
                                    on new { BatchId = m.FirstBatchId, m.EntityCode, m.SourceId }
                                    equals new { s.BatchId, s.EntityCode, s.SourceId }
                                join stage in db.Set<CollectionMigrationContractStage>().AsNoTracking() on s.Id equals stage.SourceRowId
                                join c in db.Set<CollectionContract>().AsNoTracking() on m.TargetContractId equals (long?)c.Id
                                where !c.IsDeleted && stage.TargetCustomerId == c.CustomerId
                                select new { stage.SourceCustomerId, c.CustomerId }).ToListAsync();
        var pairs = candidates.Where(x => long.TryParse(x.SourceCustomerId, out _))
            .Select(x => (LegacyId: long.Parse(x.SourceCustomerId!), x.CustomerId)).ToList();
        var parents = await db.Set<CollectionGroupParent>().AsNoTracking().ToListAsync();
        pairs.AddRange(parents.Select(x => (x.LegacyCustomerId, x.CustomerId)));
        var mapping = pairs.GroupBy(x => x.LegacyId).ToDictionary(g => g.Key, g => g.Select(x => x.CustomerId).Distinct().Order().ToArray());
        var eligible = (await CollectionCustomerScopeQuery.Customers(db.Customers.AsNoTracking()).Select(x => x.Id).ToListAsync()).ToHashSet();
        var source = await db.Database.SqlQueryRaw<Source>("""
            SELECT CAST(CommentID AS bigint) Id, CAST(CustomerID AS bigint) CustomerId,
                   Comment AS Text, CreatedBy, ModifiedBy, CreatedOn, ModifiedOn
            FROM MGS.Core.Comment ORDER BY CommentID
            """).ToListAsync();
        var existing = await db.Set<CollectionCustomerNote>().AsNoTracking().Where(x => x.LegacyCommentId != null)
            .ToDictionaryAsync(x => x.LegacyCommentId!.Value);
        var rows = source.Select(s =>
        {
            var hash = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(s));
            var targets = mapping.GetValueOrDefault(s.CustomerId) ?? [];
            var target = targets.Length == 1 ? targets[0] : (long?)null;
            string decision;
            if (existing.TryGetValue(s.Id, out var old))
                decision = old.CustomerId == target && old.SourceHash is not null && old.SourceHash.SequenceEqual(hash)
                    ? "AlreadyApplied" : "ExistingSourceOrMappingChanged";
            else if (targets.Length == 0) decision = "NoRetainedCustomerMapping";
            else if (targets.Length != 1) decision = "AmbiguousCustomerMapping";
            else if (!eligible.Contains(target!.Value)) decision = "OutsideCollectionScope";
            else if (string.IsNullOrWhiteSpace(s.Text)) decision = "EmptyNote";
            else if (s.Text.Length > 10000 || s.CreatedBy?.Length > 200 || s.ModifiedBy?.Length > 200) decision = "TextLengthReview";
            else decision = "Ready";
            return new Decision(s, target, decision, hash);
        }).ToArray();
        var planHash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(rows)));
        var counts = rows.GroupBy(x => x.Status).ToDictionary(g => g.Key, g => g.Count());
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(new { PlanHash = planHash, Counts = counts, Rows = rows },
            new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(JsonSerializer.Serialize(new { PlanHash = planHash, Counts = counts }));
        if (mode is null || mode == "--verify")
        {
            if (mode == "--verify")
            {
                var customer = rows.FirstOrDefault(x => x.TargetCustomerId.HasValue && eligible.Contains(x.TargetCustomerId.Value));
                if (customer is not null)
                {
                    var service = new CollectionCustomerService(db);
                    var card = await service.GetAsync(customer.TargetCustomerId!.Value, default);
                    var notes = await service.NotesAsync(customer.TargetCustomerId.Value, new(), default);
                    var payments = await service.PaymentsAsync(customer.TargetCustomerId.Value, new(), default);
                    if (!card.IsSuccess || !notes.IsSuccess || !payments.IsSuccess) throw new InvalidOperationException("Müşteri kartı okuma kontrolü başarısız.");
                    Console.WriteLine("Müşteri kartı, not sayfalama ve ödeme sorgusu başarılı.");
                }
            }
            Console.WriteLine("Salt okunur önizleme tamamlandı.");
            return;
        }
        if (!string.Equals(mode, planHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Önizleme değişti; aktarım yapılmadı.");
        if (rows.Any(x => x.Status == "ExistingSourceOrMappingChanged"))
            throw new InvalidOperationException("Önceden aktarılmış notun kaynak/eşleştirmesi değişmiş; aktarım durduruldu.");
        var importedAt = DateTimeOffset.UtcNow;
        foreach (var row in rows.Where(x => x.Status == "Ready"))
        {
            var s = row.Source;
            db.Add(new CollectionCustomerNote
            {
                CustomerId = row.TargetCustomerId!.Value, Text = s.Text!,
                CreatedDate = s.CreatedOn.HasValue
                    ? new DateTimeOffset(DateTime.SpecifyKind(s.CreatedOn.Value, DateTimeKind.Unspecified),
                        TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time").GetUtcOffset(DateTime.SpecifyKind(s.CreatedOn.Value, DateTimeKind.Unspecified)))
                    : importedAt,
                CreatedUser = 0, LegacyCommentId = s.Id, LegacyCustomerId = s.CustomerId,
                LegacyCreatedBy = s.CreatedBy, LegacyModifiedBy = s.ModifiedBy,
                LegacyCreatedOn = s.CreatedOn, LegacyModifiedOn = s.ModifiedOn, SourceHash = row.Hash
            });
        }
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        await File.WriteAllTextAsync(outputPath + ".applied.json", JsonSerializer.Serialize(new { PlanHash = planHash, ImportedAt = importedAt, Inserted = rows.Count(x => x.Status == "Ready") }));
        Console.WriteLine($"Aktarılan not: {rows.Count(x => x.Status == "Ready")}");
    }
    public sealed class Source
    {
        public long Id { get; set; }
        public long CustomerId { get; set; }
        public string? Text { get; set; }
        public string? CreatedBy { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? CreatedOn { get; set; }
        public DateTime? ModifiedOn { get; set; }
    }
    private sealed record Decision(Source Source, long? TargetCustomerId, string Status, byte[] Hash);
}
