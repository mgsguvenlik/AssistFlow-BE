using Business.Services.Crm.Collections.Calculation;
using Data.Concrete.EfCore.Context;
using Data.Seeding.Infrastructure;
using Data.Seeding.Seeds;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Model.Concrete.Collections;

internal static class CollectionDefinitionSeedTests
{
    public static async Task RunAsync(Func<AppDataContext> context)
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var runner = new SeedRunner([new CollectionDefinitionSeed()], services, NullLogger<SeedRunner>.Instance);
        async Task Apply()
        {
            await using var db = context();
            await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                await runner.RunAsync(db);
            });
        }
        async Task<string[]> Snapshot()
        {
            await using var db = context();
            var rows = new List<string>();
            rows.AddRange((await db.Set<CollectionPaymentFrequency>().AsNoTracking().OrderBy(x => x.Id).ToListAsync())
                .Select(x => $"frequency|{x.Id}|{x.Code}|{x.Name}|{x.IntervalMonths}|{x.DisplayOrder}|{x.IsActive}"));
            rows.AddRange((await db.Set<CollectionContractStatus>().AsNoTracking().OrderBy(x => x.Id).ToListAsync()).Select(x => $"contract|{x.Id}|{x.Code}|{x.Name}|{x.IsActive}"));
            rows.AddRange((await db.Set<CollectionSubscriptionStatus>().AsNoTracking().OrderBy(x => x.Id).ToListAsync()).Select(x => $"subscription|{x.Id}|{x.Code}|{x.Name}|{x.IsActive}"));
            rows.AddRange((await db.Set<CollectionGroupStatus>().AsNoTracking().OrderBy(x => x.Id).ToListAsync()).Select(x => $"group|{x.Id}|{x.Code}|{x.Name}|{x.IsActive}"));
            rows.AddRange((await db.Set<CollectionPaymentMethod>().AsNoTracking().OrderBy(x => x.Id).ToListAsync()).Select(x => $"method|{x.Id}|{x.Code}|{x.Name}|{x.IsActive}"));
            return rows.ToArray();
        }
        var before = await Snapshot();
        await Apply();
        var first = await Snapshot();
        await Apply();
        var second = await Snapshot();
        if (!first.SequenceEqual(second) || before.Except(first).Any())
            throw new InvalidOperationException("Seed tekrarı veya mevcut kayıtların korunması doğrulanamadı.");
        await using var verify = context();
        var frequencies = await verify.Set<CollectionPaymentFrequency>().AsNoTracking().ToListAsync();
        foreach (var id in Enumerable.Range(26, 8))
        {
            var map = LegacyPaymentFrequencyRules.Resolve(id)!;
            if (!frequencies.Any(x => x.Code == map.Code && x.IntervalMonths == map.IntervalMonths))
                throw new InvalidOperationException("Legacy ödeme dönemi eşlemesi doğrulanamadı.");
        }
        var methods = await verify.Set<CollectionPaymentMethod>().Select(x => x.Code).ToListAsync();
        var contracts = await verify.Set<CollectionContractStatus>().Select(x => x.Code).ToListAsync();
        var subscriptions = await verify.Set<CollectionSubscriptionStatus>().Select(x => x.Code).ToListAsync();
        var groups = await verify.Set<CollectionGroupStatus>().Select(x => x.Code).ToListAsync();
        if (!Enumerable.Range(48, 10).All(x => methods.Contains(LegacyCollectionDefinitionRules.PaymentMethod(x)!))
            || !Enumerable.Range(13, 3).All(x => contracts.Contains(LegacyCollectionDefinitionRules.ContractStatus(x)!))
            || !Enumerable.Range(12, 2).All(x => subscriptions.Contains(LegacyCollectionDefinitionRules.SubscriptionStatus(x)!))
            || !Enumerable.Range(1, 7).All(x => groups.Contains(LegacyCollectionDefinitionRules.GroupStatus(x)!)))
            throw new InvalidOperationException("Legacy tanım kodları hedefte eksik.");
        Console.WriteLine($"Tanım kurulumu doğrulandı: {first.Length - before.Length} yeni kayıt. İkinci çalıştırma değişiklik üretmedi; önceki kayıtlar korundu. 30 legacy tanım eşlemesi doğrulandı.");
    }
}
