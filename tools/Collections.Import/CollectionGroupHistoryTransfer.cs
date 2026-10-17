using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Business.Services.Crm.Collections;
using Data.Concrete.EfCore.Collections;
using Data.Concrete.EfCore.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Model.Concrete.Collections;
using Model.Dtos.Crm.Collections;

internal static class CollectionGroupHistoryTransfer
{
    private const string Migration = "20260927190000_AddCollectionGroupFollowUpSource";
    public static async Task RunAsync(string settingsPath, string outputPath, string? mode)
    {
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath),
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var connection = new SqlConnectionStringBuilder(settings.RootElement.GetProperty("AppSettings").GetProperty("MSSQLConnectionString").GetString());
        if (connection.DataSource != "192.168.1.8" || connection.InitialCatalog != "AssistFlowTest")
            throw new InvalidOperationException("Grup geçmişi aktarımı yalnız AssistFlowTest üzerinde çalışır.");
        await using var db = new AppDataContext(new DbContextOptionsBuilder<AppDataContext>()
            .UseSqlServer(connection.ConnectionString, o => o.CommandTimeout(180)).Options);
        if (db.Database.HasPendingModelChanges()) throw new InvalidOperationException("Model ve snapshot farklı.");
        if (mode == "--install")
        {
            var pending = (await db.Database.GetPendingMigrationsAsync()).ToArray();
            if (pending.Length > 0 && (pending.Length != 1 || pending[0] != Migration))
                throw new InvalidOperationException("Yalnız K05 migrationı uygulanabilir.");
            if (pending.Length == 1) await db.Database.MigrateAsync();
            Console.WriteLine("Grup geçmişi aktarım şeması hazır.");
            return;
        }
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var maps = await db.Set<CollectionMigrationMap>().AsNoTracking()
            .Where(x => x.SourceSystem == "MGS" && x.EntityCode == "Contract" && x.TargetContractId != null)
            .Select(x => new { x.SourceId, Target = x.TargetContractId!.Value }).ToListAsync();
        var mapping = maps.Where(x => long.TryParse(x.SourceId, out _)).GroupBy(x => long.Parse(x.SourceId))
            .ToDictionary(g => g.Key, g => g.Select(x => x.Target).Distinct().Order().ToArray());
        var eligible = (await CollectionCustomerScopeQuery.Contracts(db.Set<CollectionContract>(), db.Customers,
            CollectionCustomerClass.Group).AsNoTracking().Select(x => x.Id).ToListAsync()).ToHashSet();
        var statuses = await db.Set<CollectionGroupStatus>().AsNoTracking().ToListAsync();
        var source = await db.Database.SqlQueryRaw<Source>("""
            SELECT CAST(f.FollowGroupStatusID AS bigint) Id, CAST(f.ContractID AS bigint) ContractId,
                f.PeriodMonth, f.PeriodYear, CAST(f.GroupStatusID AS bigint) StatusId,
                s.Name AS StatusName, f.StatusDescription AS Description
            FROM MGS.Core.FollowGroupStatus f LEFT JOIN MGS.Definition.GroupStatus s ON s.GroupStatusID=f.GroupStatusID
            ORDER BY f.FollowGroupStatusID
            """).ToListAsync();
        var existing = await db.Set<CollectionContractPeriodFollowUp>().AsNoTracking().ToListAsync();
        var byLegacy = existing.Where(x => x.LegacyFollowGroupStatusId.HasValue).ToDictionary(x => x.LegacyFollowGroupStatusId!.Value);
        var existingPeriods = existing.Where(x => !x.IsDeleted).Select(x => ((long?)x.ContractId, (DateOnly?)x.Period)).ToHashSet();
        var duplicateGroups = source.Where(x => Period(x) != null).GroupBy(x => (x.ContractId, Period(x)))
            .Where(g => g.Count() > 1).ToArray();
        var duplicatePeriods = duplicateGroups.Where(g => g.Select(x => (x.StatusId, x.StatusName, x.Description)).Distinct().Count() > 1)
            .Select(g => g.Key).ToHashSet();
        // Identical states can be represented once; minimum ID is a canonical key, not a claim about chronology.
        var aliases = duplicateGroups.Where(g => g.Select(x => (x.StatusId, x.StatusName, x.Description)).Distinct().Count() == 1)
            .SelectMany(g => g.OrderBy(x => x.Id).Skip(1).Select(x => x.Id)).ToHashSet();
        var rows = source.Select(s =>
        {
            var hash = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(s));
            var targets = s.ContractId.HasValue ? mapping.GetValueOrDefault(s.ContractId.Value) ?? [] : [];
            long? target = targets.Length == 1 ? targets[0] : null;
            var period = Period(s);
            var matched = statuses.Where(x => Name(x.Name) == Name(s.StatusName)).ToArray();
            long? status = s.StatusId == null ? null : matched.Length == 1 ? matched[0].Id : null;
            string decision;
            if (byLegacy.TryGetValue(s.Id, out var old))
                decision = old.ContractId == target && old.Period == period && old.SourceHash is not null && old.SourceHash.SequenceEqual(hash)
                    ? "AlreadyApplied" : "ExistingSourceOrMappingChanged";
            else if (!s.ContractId.HasValue) decision = "MissingContractId";
            else if (targets.Length == 0) decision = "NoRetainedContractMapping";
            else if (targets.Length != 1) decision = "AmbiguousContractMapping";
            else if (!eligible.Contains(target!.Value)) decision = "OutsideGroupScope";
            else if (period is null) decision = "InvalidPeriod";
            else if (duplicatePeriods.Contains((s.ContractId, period))) decision = "DuplicatePeriodReview";
            else if (aliases.Contains(s.Id)) decision = "IdenticalDuplicateRepresentedOnce";
            else if (s.StatusId != null && status == null) decision = "StatusMappingReview";
            else if (s.Description?.Length > 500) decision = "DescriptionLengthReview";
            else if (existingPeriods.Contains((target, period))) decision = "ExistingTargetPeriodReview";
            else decision = "Ready";
            return new Decision(s, target, period, status, decision, hash);
        }).ToArray();
        // Multiple legacy contracts must never silently converge onto the same target period.
        var collisions = rows.Where(x => x.Status == "Ready").GroupBy(x => (x.TargetContractId, x.Period))
            .Where(g => g.Count() > 1).SelectMany(g => g.Select(x => x.Source.Id)).ToHashSet();
        rows = rows.Select(x => collisions.Contains(x.Source.Id) ? x with { Status = "TargetPeriodCollision" } : x).ToArray();
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
                var service = new CollectionGroupFollowUpService(db);
                var result = await service.GetHistoryAsync(new());
                if (!result.IsSuccess) throw new InvalidOperationException("Grup geçmişi okuma kontrolü başarısız.");
                foreach (var row in result.Data!.Items.Take(3))
                {
                    var detail = await service.GetAsync(row.ContractId, row.Period);
                    if (!detail.IsSuccess || detail.Data!.GroupStatusId != row.GroupStatusId || detail.Data.Description != row.Description)
                        throw new InvalidOperationException("Grup dönem detayı ve liste uyuşmuyor.");
                }
                Console.WriteLine($"Grup geçmişi liste/detay kontrolü başarılı. Toplam: {result.Data.TotalCount}");
            }
            Console.WriteLine("Salt okunur önizleme tamamlandı.");
            return;
        }
        if (!string.Equals(mode, planHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Önizleme değişti; aktarım yapılmadı.");
        if (rows.Any(x => x.Status == "ExistingSourceOrMappingChanged"))
            throw new InvalidOperationException("Önceden aktarılan kaynağın eşlemesi/içeriği değişmiş.");
        var now = DateTimeOffset.UtcNow;
        foreach (var row in rows.Where(x => x.Status == "Ready"))
            db.Add(new CollectionContractPeriodFollowUp
            {
                ContractId = row.TargetContractId!.Value, Period = row.Period!.Value,
                GroupStatusId = row.TargetStatusId, Description = row.Source.Description,
                LegacyFollowGroupStatusId = row.Source.Id, SourceHash = row.Hash,
                CreatedDate = now, CreatedUser = 0
            });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        await File.WriteAllTextAsync(outputPath + ".applied.json", JsonSerializer.Serialize(new
            { PlanHash = planHash, ImportedAt = now, Inserted = rows.Count(x => x.Status == "Ready") }));
        Console.WriteLine($"Aktarılan grup dönemi: {rows.Count(x => x.Status == "Ready")}");
    }
    private static string Name(string? name) => (name ?? "").Trim().ToUpper(CultureInfo.GetCultureInfo("tr-TR"));
    private static DateOnly? Period(Source s) => int.TryParse(s.PeriodMonth, out var m)
        && int.TryParse(s.PeriodYear, out var y) && m is >= 1 and <= 12 && y is >= 1 and < 9999 ? new(y, m, 1) : null;
    public sealed class Source
    {
        public long Id { get; set; }
        public long? ContractId { get; set; }
        public string? PeriodMonth { get; set; }
        public string? PeriodYear { get; set; }
        public long? StatusId { get; set; }
        public string? StatusName { get; set; }
        public string? Description { get; set; }
    }
    private sealed record Decision(Source Source, long? TargetContractId, DateOnly? Period, long? TargetStatusId, string Status, byte[] Hash);
}
