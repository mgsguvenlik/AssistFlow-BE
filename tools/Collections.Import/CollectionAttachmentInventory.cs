using System.Globalization;
using System.Text;
using System.Text.Json;
using Data.Concrete.EfCore.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Model.Concrete.Collections;

internal static class CollectionAttachmentInventory
{
    private static readonly HashSet<string> AllowedExtensions = [".pdf", ".png", ".jpg", ".jpeg"];

    public static async Task RunAsync(string sourceSystem, string snapshotKey, byte[] expectedManifestHash,
        string settingsPath, string snapshotDirectory)
    {
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath), new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        });
        var connection = new SqlConnectionStringBuilder(settings.RootElement.GetProperty("AppSettings")
            .GetProperty("MSSQLConnectionString").GetString());
        if (connection.DataSource != "192.168.1.8" || connection.InitialCatalog != "AssistFlowTest")
            throw new InvalidOperationException("Dosya envanteri yalnız AssistFlowTest üzerinde çalışır.");
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

        var maps = await db.Set<CollectionMigrationMap>().AsNoTracking()
            .Where(x => x.SourceSystem == sourceSystem && x.EntityCode == "Contract"
                && x.LastBatchId == batch.Id && x.TargetContractId.HasValue)
            .Select(x => new { x.SourceId, TargetContractId = x.TargetContractId!.Value }).ToListAsync();
        var targetBySource = maps.ToDictionary(x => NormalizeId(x.SourceId), x => x.TargetContractId,
            StringComparer.Ordinal);
        var stages = await db.Set<CollectionMigrationContractStage>().AsNoTracking().Include(x => x.SourceRow)
            .Where(x => x.SourceRow.BatchId == batch.Id && x.Status == CollectionMigrationRowStatus.Applied
                && x.AttachmentName != null && x.AttachmentPath != null)
            .ToListAsync();
        var pathCounts = stages.Where(x => !string.IsNullOrWhiteSpace(x.AttachmentPath))
            .GroupBy(x => NormalizePath(x.AttachmentPath!), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.OrdinalIgnoreCase);

        var reportDirectory = Path.Combine(snapshotDirectory, "reports");
        Directory.CreateDirectory(reportDirectory);
        var reportPath = Path.Combine(reportDirectory, "contract-attachment-inventory.csv");
        await using var writer = new StreamWriter(reportPath, false, new UTF8Encoding(true));
        await writer.WriteLineAsync("LegacyContractId;TargetContractId;SubscriberNo;AttachmentName;LegacyPath;Extension;PathStatus;ExtensionStatus;DuplicatePathCount;PhysicalFileStatus");
        var pathStatusCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var extensionStatusCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var stage in stages.OrderBy(x => NormalizeId(x.SourceRow.SourceId), StringComparer.Ordinal))
        {
            var sourceId = NormalizeId(stage.SourceRow.SourceId);
            if (!targetBySource.TryGetValue(sourceId, out var targetContractId))
                throw new InvalidOperationException($"Applied sözleşmenin migration map kaydı bulunamadı: {sourceId}");
            var normalizedPath = NormalizePath(stage.AttachmentPath!);
            var extension = Path.GetExtension(stage.AttachmentName)?.ToLowerInvariant() ?? string.Empty;
            var pathStatus = PathStatus(normalizedPath);
            var extensionStatus = string.IsNullOrWhiteSpace(stage.AttachmentName)
                ? "Dosya metadata yok"
                : AllowedExtensions.Contains(extension) ? "Uygun" : "Desteklenmeyen uzantı";
            var duplicateCount = string.IsNullOrWhiteSpace(normalizedPath) ? 0 : pathCounts[normalizedPath];
            pathStatusCounts[pathStatus] = pathStatusCounts.GetValueOrDefault(pathStatus) + 1;
            extensionStatusCounts[extensionStatus] = extensionStatusCounts.GetValueOrDefault(extensionStatus) + 1;
            await writer.WriteLineAsync(string.Join(';', new[]
            {
                Csv(sourceId), Csv(targetContractId.ToString(CultureInfo.InvariantCulture)), Csv(stage.SubscriberNoNormalized),
                Csv(stage.AttachmentName), Csv(stage.AttachmentPath), Csv(extension), Csv(pathStatus),
                Csv(extensionStatus), Csv(duplicateCount.ToString(CultureInfo.InvariantCulture)),
                Csv("Kaynak dosya kökü sağlanmadı")
            }));
        }
        Console.WriteLine($"Aktarılmış sözleşme dosya metadata envanteri: {stages.Count} kayıt.");
        foreach (var item in pathStatusCounts.OrderByDescending(x => x.Value))
            Console.WriteLine($"  Yol|{item.Key}|{item.Value}");
        foreach (var item in extensionStatusCounts.OrderByDescending(x => x.Value))
            Console.WriteLine($"  Uzantı|{item.Key}|{item.Value}");
        Console.WriteLine($"  Tekrarlı dolu legacy yol satırı: {stages.Count(x => !string.IsNullOrWhiteSpace(x.AttachmentPath) && pathCounts[NormalizePath(x.AttachmentPath!)] > 1)}");
        Console.WriteLine("Fiziksel dosya ve CDN aktarımı yapılmadı; kaynak dosya kökü mevcut ortamda bulunmuyor.");
        Console.WriteLine($"Rapor Git dışındaki kesit alanına yazıldı: {reportPath}");
    }

    private static string PathStatus(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "Dosya metadata yok";
        if (path.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(x => x == ".."))
            return "Güvensiz üst dizin kullanımı";
        if (!path.StartsWith("~/Content/Contract/", StringComparison.OrdinalIgnoreCase))
            return "Beklenmeyen legacy yol";
        return "Yapısal olarak uygun";
    }

    private static string NormalizePath(string value) => value.Trim().Replace('\\', '/').Replace("//", "/");

    private static string NormalizeId(string value)
    {
        var trimmed = value.Trim();
        return long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
            ? id.ToString(CultureInfo.InvariantCulture) : trimmed;
    }

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var safe = value[0] is '=' or '+' or '-' or '@' ? $"'{value}" : value;
        return $"\"{safe.Replace("\"", "\"\"")}\"";
    }
}
