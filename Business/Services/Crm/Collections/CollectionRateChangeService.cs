using System.ComponentModel.DataAnnotations;
using Business.Interfaces;
using Business.Services.Crm.Collections.Calculation;
using Core.Common;
using Core.Enums;
using Data.Concrete.EfCore.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Model.Concrete.Collections;
using Model.Dtos.Crm.Collections;

namespace Business.Services.Crm.Collections;

public sealed class CollectionRateChangeService(AppDataContext db) : ICollectionRateChangeService
{
    public async Task<ResponseModel<CollectionRateChanged>> ChangeAsync(long id, CollectionRateChange command,
        long actorId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        if (actorId <= 0) return Fail("Geçerli kullanıcı gereklidir.", StatusCode.Unauthorized);
        var errors = new List<ValidationResult>();
        if (id <= 0 || !Validator.TryValidateObject(command, new ValidationContext(command), errors, true))
            return Fail(errors.Count == 0 ? "Geçersiz sözleşme kimliği." : string.Join(" ", errors.Select(x => x.ErrorMessage)));
        if (db.ChangeTracker.Entries().Any() || db.Database.CurrentTransaction is not null)
            return Fail("İşlem şu anda tamamlanamıyor. Lütfen tekrar deneyin.", StatusCode.Conflict);

        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(now, "Europe/Istanbul").DateTime);
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
            CollectionContract? contract = null;
            CollectionContractRatePeriod? next = null;
            try
            {
                if (!await db.Users.AsNoTracking().AnyAsync(x => x.Id == actorId && !x.IsDeleted, cancellationToken))
                    return Fail("Geçerli kullanıcı bulunamadı.", StatusCode.Unauthorized);
                contract = await db.Set<CollectionContract>().SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
                if (contract is null) return Fail("Sözleşme bulunamadı.", StatusCode.NotFound);
                if (!contract.RowVersion.SequenceEqual(command.RowVersion))
                    return Fail("Sözleşme değişmiş olabilir. Detayı yenileyip tekrar deneyin.", StatusCode.Conflict);
                if (contract.StartDate > today || contract.EndDate < today)
                    return Fail("Başlamamış veya sona ermiş sözleşmede tarife değiştirilemez.");
                var subscriptionCode = await db.Set<CollectionSubscriptionStatus>().AsNoTracking()
                    .Where(x => x.Id == contract.SubscriptionStatusId).Select(x => x.Code).SingleOrDefaultAsync(cancellationToken);
                var statusCode = await db.Set<CollectionContractStatus>().AsNoTracking()
                    .Where(x => x.Id == contract.ContractStatusId).Select(x => x.Code).SingleOrDefaultAsync(cancellationToken);
                if (subscriptionCode != "ACTIVE" || !CollectionStartRules.IsIncluded(statusCode))
                    return Fail("Tarife yalnız aktif ve tahsilata dahil sözleşmede değiştirilebilir.", StatusCode.Conflict);
                var candidates = await db.Set<CollectionContractRatePeriod>()
                    .Where(x => x.ContractId == id && !x.IsDeleted && (x.EffectiveToExclusive == null || x.EffectiveToExclusive > today))
                    .OrderBy(x => x.EffectiveFrom).Take(2).ToListAsync(cancellationToken);
                if (candidates.Count != 1 || candidates[0].EffectiveFrom >= today)
                    return Fail("Tarife geçmişi uygun değil veya bugün zaten değiştirildi. Detayı kontrol edin; aynı gün tekrar değişiklik yapılamaz.", StatusCode.Conflict);
                var current = candidates[0];
                if (current.BillingBehavior is not (CollectionBillingBehavior.Billable or CollectionBillingBehavior.Free))
                    return Fail("Askıdaki tarifede tutar değiştirilemez.", StatusCode.Conflict);
                var frequency = await db.Set<CollectionPaymentFrequency>().AsNoTracking()
                    .SingleOrDefaultAsync(x => x.Id == current.PaymentFrequencyId, cancellationToken);
                if (frequency is null || !CollectionPeriodRules.IsSupportedInterval(frequency.IntervalMonths))
                    return Fail("Sözleşmenin ödeme dönemi geçersiz.");
                var endExclusive = CollectionPeriodRules.ToExclusiveEnd(contract.EndDate);
                var upper = current.EffectiveToExclusive is { } rateEnd && (endExclusive is null || rateEnd < endExclusive)
                    ? rateEnd : endExclusive;
                if (upper is null) upper = DateOnly.MaxValue;
                if (today == DateOnly.MaxValue || today.AddDays(1) >= upper)
                    return Fail("Sözleşmede sonraki bir yenileme dönemi bulunamadı.");
                var anchorDay = current.OriginalAnchorDay ?? (byte)current.BillingAnchor.Day;
                if (CollectionPeriodRules.GetDueDates(current.BillingAnchor, upper, frequency.IntervalMonths,
                    today, today.AddDays(1), anchorDay).Any())
                    return Fail("Bugün yenileme günü. Mevcut dönem borcunu değiştirmemek için tarife yarın güncellenebilir.", StatusCode.Conflict);
                var nextDue = CollectionPeriodRules.GetDueDates(current.BillingAnchor, upper, frequency.IntervalMonths,
                    today.AddDays(1), upper.Value, anchorDay).FirstOrDefault();
                if (nextDue == default)
                    return Fail("Sözleşmede sonraki bir yenileme dönemi bulunamadı.");
                if (current.CurrencyTypeId is null)
                    return Fail("Tarifede para birimi bulunamadı.");

                next = new CollectionContractRatePeriod
                {
                    ContractId = id,
                    EffectiveFrom = today,
                    EffectiveToExclusive = current.EffectiveToExclusive,
                    BillingAnchor = current.BillingAnchor,
                    OriginalAnchorDay = current.OriginalAnchorDay,
                    PaymentFrequencyId = current.PaymentFrequencyId,
                    Amount = command.Amount,
                    CurrencyTypeId = current.CurrencyTypeId,
                    BillingBehavior = command.IsFree ? CollectionBillingBehavior.Free : CollectionBillingBehavior.Billable,
                    ChangeReason = "Tarife: " + command.Reason.Trim(),
                    CreatedUser = actorId,
                    CreatedDate = now
                };
                current.EffectiveToExclusive = today;
                current.UpdatedUser = actorId;
                current.UpdatedDate = now;
                contract.UpdatedUser = actorId;
                contract.UpdatedDate = now;
                await db.SaveChangesAsync(cancellationToken);
                db.Set<CollectionContractRatePeriod>().Add(next);
                await db.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
                return ResponseModel<CollectionRateChanged>.Success(new(id, today, nextDue),
                    command.IsFree
                        ? "Tarife ücretsiz olarak güncellendi. Sonraki dönemlerde yeni borç oluşmaz."
                        : "Tarife güncellendi. Yeni tutar sonraki yenileme döneminden itibaren uygulanır.");
            }
            catch (DbUpdateConcurrencyException)
            {
                return Fail("Sözleşme başka bir işlemde değiştirildi. Detayı yenileyip tekrar deneyin.", StatusCode.Conflict);
            }
            catch (DbUpdateException)
            {
                return Fail("Tarife güncellenemedi. Detayı yenileyip tekrar deneyin.", StatusCode.Conflict);
            }
            catch (SqlException)
            {
                return Fail("İşlem sonucu doğrulanamadı. Detayı yenileyip güncel durumu kontrol edin.", StatusCode.Conflict);
            }
            catch (InvalidOperationException ex) when (ex.GetBaseException() is SqlException)
            {
                return Fail("Eşzamanlı işlem nedeniyle tarife güncellenemedi. Tekrar deneyin.", StatusCode.Conflict);
            }
            catch (TimeoutException)
            {
                return Fail("İşlem yanıtı alınamadı. Tekrar işlem yapmadan önce detayı yenileyin.", StatusCode.Conflict);
            }
            finally
            {
                if (next is not null) db.Entry(next).State = EntityState.Detached;
                foreach (var entry in db.ChangeTracker.Entries<CollectionContractRatePeriod>().ToArray()) entry.State = EntityState.Detached;
                if (contract is not null) db.Entry(contract).State = EntityState.Detached;
            }
        });
    }

    private static ResponseModel<CollectionRateChanged> Fail(string message, StatusCode status = StatusCode.BadRequest) =>
        ResponseModel<CollectionRateChanged>.Fail(message, status);
}
