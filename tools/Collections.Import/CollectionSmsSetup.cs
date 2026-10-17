using System.Text.Json;
using Data.Concrete.EfCore.Context;
using Data.Seeding.Seeds;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

internal static class CollectionSmsSetup
{
    public static async Task RunAsync(string settingsPath)
    {
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath),
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var cs = new SqlConnectionStringBuilder(settings.RootElement.GetProperty("AppSettings").GetProperty("MSSQLConnectionString").GetString());
        if (cs.DataSource != "192.168.1.8" || cs.InitialCatalog != "AssistFlowTest")
            throw new InvalidOperationException("SMS kurulumu yalnız AssistFlowTest üzerinde çalışır.");
        await using var db = new AppDataContext(new DbContextOptionsBuilder<AppDataContext>().UseSqlServer(cs.ConnectionString).Options);
        if (db.Database.HasPendingModelChanges()) throw new InvalidOperationException("SMS model/snapshot farklı.");
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToArray();
        if (pending.Any(x => !x.EndsWith("_AddCollectionSmsNotifications", StringComparison.Ordinal)))
            throw new InvalidOperationException("SMS dışı bekleyen migration var; otomatik uygulanmadı.");
        await db.Database.MigrateAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        await new CollectionSmsConfigurationSeed().RunAsync(db, null!, CancellationToken.None);
        await tx.CommitAsync();
        Console.WriteLine("AssistFlowTest SMS tabloları ve eksik şablon parametresi hazır. Mevcut şablon değiştirilmedi. Canlı/MGS değiştirilmedi; SMS gönderilmedi.");
    }
}
