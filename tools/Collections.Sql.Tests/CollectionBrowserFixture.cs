using Business.Services.Crm.Collections;
using Data.Concrete.EfCore.Context;
using Microsoft.EntityFrameworkCore;
using Model.Concrete.Collections;
using Model.Dtos.Crm.Collections;

internal static class CollectionBrowserFixture
{
    public static async Task RunAsync(Func<AppDataContext> context, bool cleanup, string key)
    {
        if (!Guid.TryParseExact(key, "N", out var requestId)) throw new InvalidOperationException("Geçerli fixture anahtarı gereklidir.");
        var marker = "UI-VERIFY-" + key;
        await using var db = context();
        if (cleanup)
        {
            await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                var matches = await db.Set<CollectionContract>().Where(x => x.CreationRequestId == requestId && x.GtsNo == marker).ToListAsync();
                if (matches.Count != 1) throw new InvalidOperationException("Tek işaretli fixture bulunamadı; temizlik durduruldu.");
                var id = matches[0].Id;
                if (await db.Set<CollectionPayment>().AnyAsync(x => x.ContractId == id)
                    || await db.Set<CollectionContractRatePeriod>().AnyAsync(x => x.ContractId == id && x.Amount != 0))
                    throw new InvalidOperationException("Fixture finansal veri içeriyor; temizlik durduruldu.");
                await db.Set<CollectionContractRatePeriod>().Where(x => x.ContractId == id).ExecuteDeleteAsync();
                await db.Set<CollectionContract>().Where(x => x.Id == id && x.CreationRequestId == requestId && x.GtsNo == marker).ExecuteDeleteAsync();
                await tx.CommitAsync();
                Console.WriteLine($"Temizlendi: yalnız tarayıcı fixture sözleşmesi {id} ve tarifeleri.");
            });
            return;
        }
        var command = new CollectionContractCreate
        {
            RequestId = requestId,
            CustomerId = await db.Customers.AsNoTracking().Where(x => !x.IsDeleted).OrderBy(x => x.Id).Select(x => x.Id).FirstAsync(),
            ServiceTypeId = await db.ServiceTypes.AsNoTracking().Where(x => !x.IsDeleted).OrderBy(x => x.Id).Select(x => x.Id).FirstAsync(),
            CurrencyTypeId = await db.CurrencyTypes.AsNoTracking().OrderBy(x => x.Id).Select(x => x.Id).FirstAsync(),
            SubscriptionStatusId = await db.Set<CollectionSubscriptionStatus>().Where(x => x.Code == "FROZEN").Select(x => x.Id).SingleAsync(),
            ContractStatusId = await db.Set<CollectionContractStatus>().Where(x => x.Code == "NONE").Select(x => x.Id).SingleAsync(),
            PaymentFrequencyId = await db.Set<CollectionPaymentFrequency>().Where(x => x.Code == "MONTHLY").Select(x => x.Id).SingleAsync(),
            StartDate = new DateOnly(2026, 1, 1), IsFree = true, Amount = 0, GtsNo = marker, IvrNo = "UI-BROWSER"
        };
        var actor = await db.Users.AsNoTracking().Where(x => !x.IsDeleted).OrderBy(x => x.Id).Select(x => x.Id).FirstAsync();
        var result = await new CollectionContractCreateService(db).CreateAsync(command, actor);
        if (result.Data is null) throw new InvalidOperationException(result.Message);
        Console.WriteLine($"Tarayıcı fixture Id={result.Data.ContractId}; GTS={marker}; ücretsiz/donuk/YOK, tutar=0.");
    }
}
