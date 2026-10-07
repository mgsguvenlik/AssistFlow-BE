using Business.Services.Sheets;
using Data.Concrete.EfCore.Context;
using Microsoft.EntityFrameworkCore;

var count = SheetSnapshotTests.Run();
Console.WriteLine("PASS snapshot limits and stable coordinate mappings");
count += await SheetTransportTests.Run();
if (args.Contains("--sql"))
{
    // This runner can only create/delete its own uniquely named LocalDB test database.
    var database = "MgsSheetsTests_" + Guid.NewGuid().ToString("N");
    var connection = $"Server=(localdb)\\MSSQLLocalDB;Database={database};Integrated Security=true;TrustServerCertificate=true";
    try
    {
        count += await new SheetsServiceTests(connection).Run();
        Console.WriteLine("PASS SQL Server migration, permissions, last-save-wins, immutable blocks, XLSX and physical deletion");
    }
    finally
    {
        if (!database.StartsWith("MgsSheetsTests_", StringComparison.Ordinal)) throw new InvalidOperationException("Unsafe test database name");
        await using var cleanup = new AppDataContext(new DbContextOptionsBuilder<AppDataContext>().UseSqlServer(connection).Options);
        await cleanup.Database.EnsureDeletedAsync();
    }
}
Console.WriteLine($"PASS {count} scenario groups");

internal static class Check
{
    public static void That(bool condition, string message) { if (!condition) throw new Exception("FAILED: " + message); }
    public static void Throws(Action action, string message)
    {
        try { action(); } catch (SheetRuleException) { return; }
        throw new Exception("FAILED expected rejection: " + message);
    }
    public static async Task Reject(Func<Task> action, int status, string message)
    {
        try { await action(); } catch (SheetRuleException ex) when (ex.Status == status) { return; }
        throw new Exception("FAILED expected " + status + ": " + message);
    }
}

