using System.Text.Json;
using Business.Services.Crm.Collections;
using Data.Concrete.EfCore.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Model.Dtos.Crm.Collections;

internal static class CollectionInvoiceSetup
{
    public static async Task RunAsync(string settingsPath, string mode)
    {
        if (mode is not ("--install" or "--verify" or "--verify-commands")) throw new InvalidOperationException("--install, --verify veya --verify-commands seçin.");
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath),
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var connection = new SqlConnectionStringBuilder(settings.RootElement.GetProperty("AppSettings").GetProperty("MSSQLConnectionString").GetString());
        if (connection.DataSource != "192.168.1.8" || connection.InitialCatalog != "AssistFlowTest")
            throw new InvalidOperationException("Fatura hazırlığı yalnız AssistFlowTest üzerinde çalışır.");
        await using var db = new AppDataContext(new DbContextOptionsBuilder<AppDataContext>()
            .UseSqlServer(connection.ConnectionString, o => o.CommandTimeout(60)).Options);
        if (db.Database.HasPendingModelChanges()) throw new InvalidOperationException("Model ve snapshot farklı.");
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToArray();
        if (mode == "--install")
        {
            if (pending.Any(x => x is not ("20260929080736_AddCollectionInvoices" or "20260929084242_AddCollectionInvoiceOperations")))
                throw new InvalidOperationException("Yalnız K06 fatura şeması uygulanabilir.");
            if (pending.Length > 0) await db.Database.MigrateAsync();
            Console.WriteLine("K06 fatura şeması hazır. Veri aktarımı yapılmadı.");
        }
        else if (pending.Length != 0) throw new InvalidOperationException("Önce bekleyen şemayı hazırlayın.");
        if (mode == "--verify-commands")
        {
            await CollectionInvoiceVerification.RunAsync(db);
            return;
        }
        var service = new CollectionInvoiceService(db);
        foreach (var type in new string?[] { null, "B", "K" })
            foreach (var paid in new bool?[] { null, true, false })
            {
                var result = await service.ListAsync(new CollectionInvoiceQuery
                {
                    Type = type, Paid = paid, Search = "K06", From = new DateOnly(2000, 1, 1), To = new DateOnly(2100, 1, 1)
                }, default);
                if (!result.IsSuccess) throw new InvalidOperationException("Fatura sorgusu doğrulanamadı.");
            }
        await service.GetAsync(long.MaxValue, default);
        await service.PaymentsAsync(long.MaxValue, new CollectionRateHistoryQuery(), default);
        Console.WriteLine("Liste, filtre ve bulunamayan detay sorguları SQL Server üzerinde doğrulandı.");
    }
}
