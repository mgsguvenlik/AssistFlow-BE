using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Data.Concrete.EfCore.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

// No write/apply command exists. Connections only run fixed SELECTs for capture/evidence.
if (args.Length == 8 && args[0] == "seal-cutover")
{
    await CutoverBundle.RunAsync(args[1], args[2], args[3], args[4], args[5], args[6], args[7]);
    return;
}
if (args.Length == 3 && args[0] == "preview-rehearsal")
{
    await LiveTransferPreview.RunAsync(args[1], args[2], true);
    return;
}
if (args.Length == 5 && args[0] == "prepare-rehearsal")
{
    await RehearsalPreparation.RunAsync(args[1], args[2], args[3], args[4]);
    return;
}
if (args.Length == 8 && args[0] == "reconcile-relations")
{
    await LiveRelationReconciliation.RunAsync(args[1], args[2], args[3], args[4], args[5], args[6], args[7]);
    return;
}
if (args.Length == 5 && args[0] == "reconcile-payments")
{
    await LivePaymentReconciliation.RunAsync(args[1], args[2], args[3], args[4]);
    return;
}
if (args.Length == 3 && args[0] == "preview-live")
{
    await LiveTransferPreview.RunAsync(args[1], args[2]);
    return;
}
if (args.Length is 3 or 4 && args[0] == "review-live")
{
    await LiveFinancialPreview.RunAsync(args[1], args[2], args.Length == 4 ? args[3] : null);
    return;
}
if (args.Length == 4 && args[0] == "replay-decisions")
{
    await CustomerDecisionReplay.RunAsync(args[1], args[2], args[3]);
    return;
}
if (args.Length is 5 or 6 or 7 && args[0] == "prepare-data")
{
    await ProductionDataPackage.RunAsync(args[1], args[2], args[3], args[4], args.Length >= 6 ? args[5] : null, args.Length == 7 ? args[6] : null);
    return;
}
if (args.Length != 3 || args[0] != "build")
    throw new ArgumentException("Kullanım: build <ön kontrol JSON> <yeni çıktı> | preview-live <canlı config> <yeni .tools klasörü> | review-live <okuma paketi> <yeni çıktı> [karar paketi] | replay-decisions <okuma paketi> <karar arşivi> <yeni çıktı> | prepare-data <okuma paketi> <inceleme paketi> <canlı config> <yeni çıktı> [ödeme mutabakatı] [ilişki mutabakatı] | reconcile-payments <okuma paketi> <inceleme paketi> <canlı config> <yeni çıktı> | reconcile-relations <okuma paketi> <inceleme paketi> <canlı config> <Contract kökü> <dosya manifesti> <Aktivite arşivi> <yeni çıktı>. Hiçbir komut DB'ye yazmaz.");
var profilePath = Path.GetFullPath(args[1]);
var outputPath = Path.GetFullPath(args[2]);
if (Directory.Exists(outputPath) || File.Exists(outputPath)) throw new InvalidOperationException("Mevcut yayın paketi üzerine yazılmaz.");
var profileBytes = await File.ReadAllBytesAsync(profilePath);
using var profile = JsonDocument.Parse(profileBytes);
if (profile.RootElement.ValueKind != JsonValueKind.Array || !profile.RootElement.EnumerateArray().Any(x =>
    x.GetProperty("Section").GetString() == "Environment" && x.GetProperty("DatabaseName").GetString() == "AssistFlow"))
    throw new InvalidOperationException("Doğrulanmış AssistFlow ön kontrol raporu gereklidir.");
var applied = profile.RootElement.EnumerateArray().Where(x => x.GetProperty("Section").GetString() == "Migrations")
    .Select(x => x.GetProperty("MigrationId").GetString()!).ToHashSet(StringComparer.Ordinal);
await using var db = new AppDataContext(new DbContextOptionsBuilder<AppDataContext>()
    .UseSqlServer("Server=localhost;Database=AssistFlowDesignTime;Integrated Security=true;TrustServerCertificate=true").Options);
var ids = db.GetService<IMigrationsAssembly>().Migrations.Keys.Order(StringComparer.Ordinal).ToArray();
var migrator = db.GetService<IMigrator>();
Directory.CreateDirectory(outputPath);
var files = new List<object>();
var plannedIds = new HashSet<string>(StringComparer.Ordinal);
for (var i = 0; i < ids.Length; i++)
{
    var id = ids[i];
    if (!id.Contains("Collection", StringComparison.Ordinal) || applied.Contains(id)) continue;
    var previous = i == 0 ? "0" : ids[i - 1];
    if (previous != "0" && !applied.Contains(previous) && !plannedIds.Contains(previous))
        throw new InvalidOperationException($"{id} öncesindeki {previous} hazır değil; otomatik devam edilmedi.");
    var sql = "-- İNCELEME PAKETİ: Yedek/prova ve onay olmadan çalıştırmayın.\n"
        + "SET XACT_ABORT ON;\nSET LOCK_TIMEOUT 15000;\n"
        + "IF DB_NAME() <> N'AssistFlow' THROW 51000, N'Yanlış hedef DB.', 1;\nGO\n"
        + "IF ISNULL(OBJECT_ID(N'__EFMigrationsHistory'),-1)<>OBJECT_ID(N'dbo.__EFMigrationsHistory') THROW 51000, N'Migration varsayılan şeması uyuşmuyor.', 1;\nGO\n"
        + $"IF NOT EXISTS(SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId=N'{previous}') THROW 51000, N'Önceki migration eksik.', 1;\nGO\n"
        + migrator.GenerateScript(previous, id, MigrationsSqlGenerationOptions.Idempotent);
    var name = $"{files.Count + 1:D2}-{id}.sql";
    var bytes = new UTF8Encoding(false).GetBytes(sql);
    await File.WriteAllBytesAsync(Path.Combine(outputPath, name), bytes);
    files.Add(new
    {
        migrationId = id,
        previousMigrationId = previous,
        file = name,
        sha256 = Convert.ToHexString(SHA256.HashData(bytes))
    });
    plannedIds.Add(id);
}
var manifest = new
{
    status = "REVIEW_REQUIRED_NOT_APPROVED_FOR_EXECUTION",
    generatedAtUtc = DateTimeOffset.UtcNow,
    targetDatabase = "AssistFlow",
    profileSha256 = Convert.ToHexString(SHA256.HashData(profileBytes)),
    note = "Yalnız eksik Collection migrationları. Seed, veri aktarımı, ortak müşteri/prosedür/yetki değişiklikleri dahil değildir. SQL istemcisi ilk hatada durmalıdır.",
    appliedCollection = ids.Where(x => x.Contains("Collection") && applied.Contains(x)).ToArray(),
    unknownExistingMigrations = applied.Except(ids).Order().ToArray(),
    pendingNonCollection = ids.Where(x => !x.Contains("Collection") && !applied.Contains(x)).ToArray(),
    files
};
await File.WriteAllTextAsync(Path.Combine(outputPath, "manifest.json"), JsonSerializer.Serialize(manifest,
    new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
Console.WriteLine($"{files.Count} eksik Tahsilat migration scripti ve SHA-256 manifesti üretildi. DB bağlantısı/yazması yok.");
