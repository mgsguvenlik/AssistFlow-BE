using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Data.Concrete.EfCore.Collections;
using Data.Concrete.EfCore.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Model.Concrete.Collections;

internal static class CollectionActivityPreview
{
    // Read-only archive/mapping inspection. Never extracts, uploads or changes customer ownership.
    public static async Task RunAsync(string settingsPath, string archive)
    {
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var cs = new SqlConnectionStringBuilder(settings.RootElement.GetProperty("AppSettings").GetProperty("MSSQLConnectionString").GetString());
        if (cs.DataSource != "192.168.1.8" || cs.InitialCatalog != "AssistFlowTest") throw new InvalidOperationException("Yalnız AssistFlowTest eşlemeleri okunabilir.");
        var start = new ProcessStartInfo("7z") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8 };
        foreach (var arg in new[] { "l", "-slt", "-sccUTF-8", Path.GetFullPath(archive) }) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var outputTask = process.StandardOutput.ReadToEndAsync(); var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(); var output = await outputTask; await errorTask;
        if (process.ExitCode != 0) throw new InvalidOperationException("Arşiv listelenemedi.");
        var files = new List<long>(); var thumbs = 0; var other = 0;
        foreach (var entry in Regex.Split(output.Replace("\r", ""), "\n\n"))
        {
            if (!Regex.IsMatch(entry, "^Folder = -$", RegexOptions.Multiline)) continue;
            var path = Regex.Match(entry, "^Path = (.+)$", RegexOptions.Multiline).Groups[1].Value.Replace('\\', '/');
            if (path.Split('/').Contains("..") || Regex.IsMatch(entry, "^(Symbolic Link|Hard Link|Copy Link) = .+", RegexOptions.Multiline)) throw new InvalidOperationException("Arşiv güvenli olmayan yol/bağlantı içeriyor.");
            var match = Regex.Match(path, @"^Aktivite/(\d+)/[^/]+$");
            if (match.Success && long.TryParse(match.Groups[1].Value, out var id)) files.Add(id);
            else if (path.StartsWith("Aktivite/Thumb/", StringComparison.OrdinalIgnoreCase)) thumbs++;
            else other++;
        }
        await using var db = new AppDataContext(new DbContextOptionsBuilder<AppDataContext>().UseSqlServer(cs.ConnectionString).Options);
        var sourceIds = files.Distinct().Select(x => x.ToString()).ToArray();
        var pairs = await (from m in db.Set<CollectionMigrationMap>().AsNoTracking()
                           join s in db.Set<CollectionMigrationSourceRow>().AsNoTracking() on new { BatchId = m.FirstBatchId, m.EntityCode, m.SourceId } equals new { s.BatchId, s.EntityCode, s.SourceId }
                           join stage in db.Set<CollectionMigrationContractStage>().AsNoTracking() on s.Id equals stage.SourceRowId
                           join c in db.Set<CollectionContract>().AsNoTracking() on m.TargetContractId equals (long?)c.Id
                           where !c.IsDeleted && stage.TargetCustomerId == c.CustomerId && sourceIds.Contains(stage.SourceCustomerId!)
                           select new { stage.SourceCustomerId, c.CustomerId }).ToListAsync();
        var map = pairs.Select(x => (Legacy: long.Parse(x.SourceCustomerId!), x.CustomerId)).ToList();
        var ids = files.Distinct().ToArray();
        map.AddRange((await db.Set<CollectionGroupParent>().AsNoTracking().Where(x => ids.Contains(x.LegacyCustomerId)).ToListAsync()).Select(x => (x.LegacyCustomerId, x.CustomerId)));
        var targetIds = map.Select(x => x.CustomerId).Distinct().ToArray();
        var scope = await CollectionCustomerScopeQuery.Customers(db.Customers.AsNoTracking()).Where(x => targetIds.Contains(x.Id)).Select(x => x.Id).ToListAsync();
        foreach (var group in files.GroupBy(x => x).OrderBy(x => x.Key))
        {
            var targets = map.Where(x => x.Legacy == group.Key).Select(x => x.CustomerId).Distinct().ToArray();
            var result = targets.Length == 0 ? "Korunan müşteri eşlemesi yok" : targets.Length > 1 ? "Birden fazla hedef" : !scope.Contains(targets[0]) ? "Tahsilat kapsamı dışında" : "Kesin eşleme";
            Console.WriteLine($"Legacy müşteri {group.Key}: {group.Count()} dosya; {result}; hedef: {string.Join(',', targets)}");
            if (targets.Length == 0)
            {
                var source = await db.Database.SqlQuery<LegacyCustomer>($"SELECT CAST(CustomerID AS bigint) Id, SubscriberNo, Type FROM MGS.Core.Customer WHERE CustomerID = {group.Key}").SingleOrDefaultAsync();
                var subscriber = source?.SubscriberNo?.Trim();
                var possible = string.IsNullOrWhiteSpace(subscriber) ? [] : await db.Customers.AsNoTracking().Where(x => !x.IsDeleted && x.SubscriberCode == subscriber)
                    .Select(x => new { x.Id, GroupCode = x.CustomerGroup == null ? null : x.CustomerGroup.Code }).ToListAsync();
                var stages = await db.Set<CollectionMigrationContractStage>().AsNoTracking().Where(x => x.SourceCustomerId == group.Key.ToString())
                    .Select(x => new { x.SourceRowId, x.Status }).ToListAsync();
                var rowIds = stages.Select(x => x.SourceRowId).ToArray();
                var issues = await db.Set<CollectionMigrationIssue>().AsNoTracking().Where(x => rowIds.Contains(x.SourceRowId))
                    .Select(x => x.IssueCode).Distinct().ToListAsync();
                Console.WriteLine($"  Legacy kayıt: {(source == null ? "yok" : source.Type)}; sözleşme stage: {string.Join(',', stages.Select(x => x.Status).Distinct())}; neden: {string.Join(',', issues)}; abone no adayları (otomatik eşleme değil): {string.Join(';', possible.Select(x => $"{x.Id}/{x.GroupCode}"))}");
                if (group.Key is 924 or 2206)
                {
                    var decisions = await db.Set<CollectionMigrationIssue>().AsNoTracking().Where(x => rowIds.Contains(x.SourceRowId) &&
                        (x.IssueCode == "CUSTOMER_DUPLICATE_SUPERSEDED" || x.IssueCode == "CUSTOMER_IMPORTED_SUPERSEDED" || x.IssueCode == "CUSTOMER_NAME_SUPERSEDED"))
                        .Select(x => new { x.Details, x.ResolutionNote }).ToListAsync();
                    foreach (var decision in decisions) Console.WriteLine($"  Tekilleştirme kararı: {decision.ResolutionNote ?? decision.Details}");
                    var winner = group.Key == 924 ? "291401" : "87400";
                    var retained = await (from m in db.Set<CollectionMigrationMap>().AsNoTracking()
                                          join s in db.Set<CollectionMigrationSourceRow>().AsNoTracking() on new { BatchId = m.FirstBatchId, m.EntityCode, m.SourceId } equals new { s.BatchId, s.EntityCode, s.SourceId }
                                          join stage in db.Set<CollectionMigrationContractStage>().AsNoTracking() on s.Id equals stage.SourceRowId
                                          join c in db.Set<CollectionContract>().AsNoTracking() on m.TargetContractId equals (long?)c.Id
                                          where stage.SourceCustomerId == winner && !c.IsDeleted && stage.TargetCustomerId == c.CustomerId
                                          select c.CustomerId).Distinct().ToListAsync();
                    Console.WriteLine($"  Korunan legacy {winner} için aktarılmış hedefler: {string.Join(',', retained)}");
                    var winnerId = long.Parse(winner);
                    var winnerSource = await db.Database.SqlQuery<LegacyCustomer>($"SELECT CAST(CustomerID AS bigint) Id, SubscriberNo, Type FROM MGS.Core.Customer WHERE CustomerID = {winnerId}").SingleOrDefaultAsync();
                    var winnerSubscriber = winnerSource?.SubscriberNo?.Trim();
                    var winnerTargets = string.IsNullOrWhiteSpace(winnerSubscriber) ? [] : await CollectionCustomerScopeQuery.Customers(db.Customers.AsNoTracking())
                        .Where(x => x.SubscriberCode == winnerSubscriber).Select(x => x.Id).ToListAsync();
                    var winnerStages = await db.Set<CollectionMigrationContractStage>().AsNoTracking().Where(x => x.SourceCustomerId == winner)
                        .Select(x => new { x.Status, x.TargetCustomerId }).Distinct().ToListAsync();
                    Console.WriteLine($"  Korunan müşteri abone no ile kapsam içi hedefler: {string.Join(',', winnerTargets)}; stage: {JsonSerializer.Serialize(winnerStages)}");
                    if (winnerTargets.Count == 0 && !string.IsNullOrWhiteSpace(winnerSubscriber))
                    {
                        var allTargets = await db.Customers.AsNoTracking().Where(x => x.SubscriberCode != null && x.SubscriberCode.Trim() == winnerSubscriber)
                            .Select(x => new { x.Id, x.IsDeleted, GroupCode = x.CustomerGroup == null ? null : x.CustomerGroup.Code, TypeCode = x.CustomerType == null ? null : x.CustomerType.Code }).ToListAsync();
                        Console.WriteLine($"  Korunan müşterinin kapsam filtresiz test adayları: {JsonSerializer.Serialize(allTargets)}");
                    }
                }
            }
        }
        using var stream = File.OpenRead(archive);
        Console.WriteLine($"Arşiv SHA256: {Convert.ToHexString(await SHA256.HashDataAsync(stream))}");
        Console.WriteLine($"Asıl dosya: {files.Count}; müşteri klasörü: {ids.Length}; küçük resim: {thumbs}; inceleme gereken diğer yol: {other}. Veritabanı/CDN değişmedi.");
    }
    private sealed class LegacyCustomer { public long Id { get; set; } public string? SubscriberNo { get; set; } public string? Type { get; set; } }
}
