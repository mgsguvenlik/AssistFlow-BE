using Business.Services.Crm.Collections;
using Business.Services.Crm.Collections.Calculation;
using Data.Concrete.EfCore.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Model.Concrete.Collections;
using Model.Dtos.Crm.Collections;

internal static class CollectionContractCreateTests
{
    public static async Task RunAsync(Func<IInterceptor?, AppDataContext> context)
    {
        await using var read = context(null);
        var actor = await read.Users.Where(x => !x.IsDeleted).Select(x => x.Id).FirstAsync();
        var customer = await read.Customers.Where(x => !x.IsDeleted).Select(x => x.Id).FirstAsync();
        var service = await read.ServiceTypes.Where(x => !x.IsDeleted).Select(x => x.Id).FirstAsync();
        var currency = await read.CurrencyTypes.Select(x => x.Id).FirstAsync();
        var active = await read.Set<CollectionSubscriptionStatus>().SingleAsync(x => x.Code == "ACTIVE");
        var frozen = await read.Set<CollectionSubscriptionStatus>().SingleAsync(x => x.Code == "FROZEN");
        var exists = await read.Set<CollectionContractStatus>().SingleAsync(x => x.Code == "EXISTS");
        var none = await read.Set<CollectionContractStatus>().SingleAsync(x => x.Code == "NONE");
        var monthly = await read.Set<CollectionPaymentFrequency>().SingleAsync(x => x.Code == "MONTHLY");
        var marker = "CREATE-TEST-" + Guid.NewGuid().ToString("N");
        var keys = new List<Guid>();
        CollectionContractCreate Command()
        {
            var key = Guid.NewGuid(); keys.Add(key);
            return new() { RequestId = key, CustomerId = customer, ServiceTypeId = service, CurrencyTypeId = currency,
                ContractStatusId = exists.Id, SubscriptionStatusId = active.Id, PaymentFrequencyId = monthly.Id,
                StartDate = new(2026, 1, 31), Amount = 3000m, GtsNo = marker };
        }
        var checks = 0;
        void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); checks++; Console.WriteLine("PASS: " + name); }
        async Task<Core.Common.ResponseModel<CollectionContractCreated>> Save(CollectionContractCreate c, IInterceptor? interceptor = null)
        {
            await using var db = context(interceptor);
            return await new CollectionContractCreateService(db).CreateAsync(c, actor);
        }
        try
        {
            var command = Command();
            var created = await Save(command);
            Check(created.Data is { Replayed: false }, "Sözleşme ve tarife oluşturuldu");
            var id = created.Data!.ContractId;
            var stored = await read.Set<CollectionContract>().AsNoTracking().SingleAsync(x => x.Id == id);
            var rate = await read.Set<CollectionContractRatePeriod>().AsNoTracking().SingleAsync(x => x.ContractId == id);
            Check(stored.StartDate == command.StartDate && rate.BillingAnchor == new DateOnly(2026, 2, 28) && rate.OriginalAnchorDay == 31,
                "İmza tarihi ve ertelenmiş yenileme günü ayrı korundu");
            var charges = CollectionAccrualRules.Calculate([CollectionRateSegmentMapper.FromPersisted(rate, monthly.IntervalMonths)],
                new(2026,1,1), new(2026,4,1));
            Check(charges.Select(x => x.DueDate).SequenceEqual(new[] { new DateOnly(2026,2,28), new DateOnly(2026,3,31) }), "Kalıcı tarife doğru yenileme tarihleri üretir");
            Check((await Save(command)).Data is { Replayed: true }, "Aynı istek yeni sözleşme oluşturmaz");
            command.Amount = 4000m;
            Check((int)(await Save(command)).StatusCode == 409, "Aynı anahtarla farklı tutar reddedilir");
            foreach (var mode in new[] { "none", "blank", "frozen", "free", "short" })
            {
                var c = Command();
                if (mode == "none") c.ContractStatusId = none.Id;
                if (mode == "blank") c.ContractStatusId = null;
                if (mode == "frozen") c.SubscriptionStatusId = frozen.Id;
                if (mode == "free") c.IsFree = true;
                if (mode == "short") c.EndDate = new(2026,2,10);
                var result = await Save(c);
                Check(result.Data is not null, mode + " sözleşmesi kaydedilir");
                var r = await read.Set<CollectionContractRatePeriod>().AsNoTracking().SingleAsync(x => x.ContractId == result.Data!.ContractId);
                Check(r.BillingBehavior != CollectionBillingBehavior.Billable, mode + " ilk tarife borç üretmez");
            }
            var failure = Command();
            var injected = false;
            try { await Save(failure, new FailRate()); } catch (InjectedFailure) { injected = true; }
            Check(injected && !await read.Set<CollectionContract>().AnyAsync(x => x.CreationRequestId == failure.RequestId), "Tarife hatası sözleşmeyi de geri alır");
            var lost = new LoseCommitAcknowledgement();
            Check((await Save(Command(), lost)).Data is { Replayed: true } && lost.Injected, "Commit yanıt kaybı tekrarı güvenli");
            var concurrent = Command();
            var barrier = new CreateBarrier();
            var pair = await Task.WhenAll(Save(concurrent, barrier), Save(concurrent, barrier));
            Check(pair.All(x => x.Data != null) && pair.Count(x => x.Data!.Replayed) == 1
                && await read.Set<CollectionContract>().CountAsync(x => x.CreationRequestId == concurrent.RequestId) == 1,
                "Eşzamanlı aynı istek tek sözleşme üretir");
            var invalid = Command(); invalid.CustomerId = long.MaxValue;
            Check((int)(await Save(invalid)).StatusCode == 400
                && !await read.Set<CollectionContract>().AnyAsync(x => x.CreationRequestId == invalid.RequestId), "Geçersiz müşteri kayıt oluşturmaz");
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "Europe/Istanbul").DateTime);
            async Task<Core.Common.ResponseModel<CollectionSubscriptionChanged>> Change(long contractId, bool freeze, byte[] version, IInterceptor? interceptor = null)
            {
                await using var db = context(interceptor);
                return await new CollectionSubscriptionService(db).ChangeAsync(contractId,
                    new() { Freeze = freeze, RowVersion = version, Reason = "SQL kabul testi" }, actor);
            }
            var before = await read.Set<CollectionContract>().AsNoTracking().SingleAsync(x => x.Id == id);
            Check((await Change(id, true, before.RowVersion)).Data is not null, "Aktif sözleşme dondurulur");
            var frozenRates = await read.Set<CollectionContractRatePeriod>().AsNoTracking().Where(x => x.ContractId == id).OrderBy(x => x.EffectiveFrom).ToListAsync();
            Check(frozenRates.Count == 2 && frozenRates[0].EffectiveToExclusive == today
                && frozenRates[1].EffectiveFrom == today && frozenRates[1].BillingBehavior == CollectionBillingBehavior.Suspended,
                "Dondurma eski tarife tutarını koruyup kesintisiz yeni dönem açar");
            var freezeCharges = CollectionAccrualRules.Calculate(frozenRates.Select(x => CollectionRateSegmentMapper.FromPersisted(x, monthly.IntervalMonths)), today, today.AddMonths(2));
            Check(freezeCharges.Count == 0, "Donuk dönemde yeni borç oluşmaz");
            Check((int)(await Change(id, true, before.RowVersion)).StatusCode == 409
                && await read.Set<CollectionContractRatePeriod>().CountAsync(x => x.ContractId == id) == 2,
                "Tekrar gönderim ikinci tarife oluşturmaz");
            foreach (var mode in new[] { "paid", "free", "none" })
            {
                var c = Command(); c.SubscriptionStatusId = frozen.Id;
                c.IsFree = mode == "free";
                if (mode == "none") c.ContractStatusId = none.Id;
                var saved = await Save(c);
                var contractId = saved.Data!.ContractId;
                var initial = await read.Set<CollectionContract>().AsNoTracking().SingleAsync(x => x.Id == contractId);
                Check((await Change(contractId, false, initial.RowVersion)).Data is not null, mode + " donuk sözleşmesi aktifleştirilir");
                var timeline = await read.Set<CollectionContractRatePeriod>().AsNoTracking().Where(x => x.ContractId == contractId).OrderBy(x => x.EffectiveFrom).ToListAsync();
                Check(timeline.Count == 2 && timeline[1].BillingAnchor == today && timeline[1].OriginalAnchorDay == today.Day,
                    mode + " aktifleştirmede yenileme günü bugündür");
                var actual = CollectionAccrualRules.Calculate(timeline.Select(x => CollectionRateSegmentMapper.FromPersisted(x, monthly.IntervalMonths)), c.StartDate, today.AddDays(1));
                Check(mode == "paid" ? actual.Count == 1 && actual[0].DueDate == today && actual[0].Amount == 3000m : actual.Count == 0,
                    mode + " geçmiş donuk borçlar üretilmez, ücretsiz ve YOK korunur");
            }
            var rollbackContract = (await Save(Command())).Data!.ContractId;
            var rollbackBefore = await read.Set<CollectionContract>().AsNoTracking().SingleAsync(x => x.Id == rollbackContract);
            var rollbackInjected = false;
            try { await Change(rollbackContract, true, rollbackBefore.RowVersion, new FailRate()); } catch (InjectedFailure) { rollbackInjected = true; }
            var rollbackAfter = await read.Set<CollectionContract>().AsNoTracking().SingleAsync(x => x.Id == rollbackContract);
            Check(rollbackInjected && rollbackAfter.RowVersion.SequenceEqual(rollbackBefore.RowVersion)
                && await read.Set<CollectionContractRatePeriod>().CountAsync(x => x.ContractId == rollbackContract) == 1,
                "Yeni tarife kaydı hatası durum değişikliğini ve eski dönem kapamasını geri alır");
            var lostContract = (await Save(Command())).Data!.ContractId;
            var lostBefore = await read.Set<CollectionContract>().AsNoTracking().SingleAsync(x => x.Id == lostContract);
            var subscriptionLost = new LoseCommitAcknowledgement();
            Check((int)(await Change(lostContract, true, lostBefore.RowVersion, subscriptionLost)).StatusCode == 409 && subscriptionLost.Injected,
                "Durum değişikliğinde commit yanıt kaybı yenileme mesajı döndürür");
            Check((int)(await Change(lostContract, true, lostBefore.RowVersion)).StatusCode == 409
                && await read.Set<CollectionContractRatePeriod>().CountAsync(x => x.ContractId == lostContract) == 2,
                "Kayıp yanıt sonrası tekrar ikinci durum değişikliği yaratmaz");
            var raceContract = (await Save(Command())).Data!.ContractId;
            var raceBefore = await read.Set<CollectionContract>().AsNoTracking().SingleAsync(x => x.Id == raceContract);
            var race = await Task.WhenAll(Change(raceContract, true, raceBefore.RowVersion), Change(raceContract, true, raceBefore.RowVersion));
            Check(race.Count(x => x.Data is not null) == 1 && race.Count(x => (int)x.StatusCode == 409) == 1
                && await read.Set<CollectionContractRatePeriod>().CountAsync(x => x.ContractId == raceContract) == 2,
                "Eşzamanlı durum değişikliklerinden yalnız biri kaydedilir");
            var sameDay = await read.Set<CollectionContract>().AsNoTracking().SingleAsync(x => x.Id == raceContract);
            Check((int)(await Change(raceContract, false, sameDay.RowVersion)).StatusCode == 409,
                "Aynı gün ikinci değişiklik sıfır uzunlukta tarife oluşturmaz");
            Console.WriteLine($"{checks} sözleşme SQL kontrolü geçti.");
        }
        finally
        {
            await using var cleanup = context(null);
            await cleanup.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var tx = await cleanup.Database.BeginTransactionAsync();
                var ids = await cleanup.Set<CollectionContract>().Where(x => x.GtsNo == marker && x.CreationRequestId != null && keys.Contains(x.CreationRequestId.Value)).Select(x => x.Id).ToListAsync();
                await cleanup.Set<CollectionContractRatePeriod>().Where(x => ids.Contains(x.ContractId)).ExecuteDeleteAsync();
                await cleanup.Set<CollectionContract>().Where(x => ids.Contains(x.Id) && x.GtsNo == marker).ExecuteDeleteAsync();
                await tx.CommitAsync();
            });
            Console.WriteLine("Yalnız oluşturma testinin sözleşme ve tarifeleri temizlendi.");
        }
    }
    private sealed class FailRate : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<CollectionContractRatePeriod>().Any(x => x.State == EntityState.Added)) throw new InjectedFailure();
            return ValueTask.FromResult(result);
        }
    }
    private sealed class CreateBarrier : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int count;
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<CollectionContract>().Any(x => x.State == EntityState.Added))
            {
                if (Interlocked.Increment(ref count) >= 2) ready.TrySetResult();
                await ready.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            }
            return result;
        }
    }
}
