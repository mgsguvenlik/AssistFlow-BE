using System.ComponentModel.DataAnnotations;
using Business.Interfaces;
using Business.Services.Crm.Collections.Calculation;
using Core.Common;
using Core.Enums;
using Data.Concrete;
using Data.Concrete.EfCore.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Model.Concrete.Collections;
using Model.Dtos.Crm.Collections;

namespace Business.Services.Crm.Collections;

/// <summary>Day-based transitions. An old version is never replayed as a new transition.</summary>
public sealed class CollectionSubscriptionService(AppDataContext db) : ICollectionSubscriptionService
{
    public async Task<ResponseModel<CollectionSubscriptionChanged>> ChangeAsync(long id, CollectionSubscriptionChange command,
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
            CollectionContractRatePeriod? current = null;
            CollectionContractRatePeriod? next = null;
            try
            {
                if (!await db.Users.AsNoTracking().AnyAsync(x => x.Id == actorId && !x.IsDeleted, cancellationToken))
                    return Fail("Geçerli kullanıcı bulunamadı.", StatusCode.Unauthorized);
                contract = await db.Set<CollectionContract>().SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
                if (contract is null) return Fail("Sözleşme bulunamadı.", StatusCode.NotFound);
                if (!contract.RowVersion.SequenceEqual(command.RowVersion))
                    return Fail("Sözleşme değişmiş veya önceki işlem tamamlanmış olabilir. Detayı yenileyip güncel durumu kontrol edin.", StatusCode.Conflict);
                var source = await db.Set<CollectionSubscriptionStatus>().AsNoTracking()
                    .SingleOrDefaultAsync(x => x.Id == contract.SubscriptionStatusId, cancellationToken);
                if (source?.Code != (command.Freeze ? "ACTIVE" : "FROZEN"))
                    return Fail(command.Freeze ? "Yalnız aktif abonelik dondurulabilir." : "Yalnız donuk abonelik aktifleştirilebilir.", StatusCode.Conflict);
                if (contract.StartDate > today || contract.EndDate < today)
                    return Fail("Başlamamış veya sona ermiş sözleşmede bu işlem yapılamaz.");
                var target = await db.Set<CollectionSubscriptionStatus>().AsNoTracking()
                    .SingleOrDefaultAsync(x => x.Code == (command.Freeze ? "FROZEN" : "ACTIVE") && x.IsActive, cancellationToken);
                if (target is null) return Fail("Hedef abonelik durumu tanımı bulunamadı.");
                var candidates = await db.Set<CollectionContractRatePeriod>()
                    .Where(x => x.ContractId == id && !x.IsDeleted && (x.EffectiveToExclusive == null || x.EffectiveToExclusive > today))
                    .OrderBy(x => x.EffectiveFrom).Take(2).ToListAsync(cancellationToken);
                if (candidates.Count != 1) return Fail("Tarife geçmişi eksik veya ileri tarihli değişiklik içeriyor. Önce tarife geçmişini kontrol edin.", StatusCode.Conflict);
                current = candidates[0];
                if (current.EffectiveFrom >= today)
                    return Fail("Tarife henüz başlamamış veya bugün değiştirilmiş. Günlük tarife geçmişini korumak için işlem sonraki gün yapılabilir.", StatusCode.Conflict);
                var frequency = await db.Set<CollectionPaymentFrequency>().AsNoTracking()
                    .SingleOrDefaultAsync(x => x.Id == current.PaymentFrequencyId, cancellationToken);
                if (frequency is null || !CollectionPeriodRules.IsSupportedInterval(frequency.IntervalMonths))
                    return Fail("Sözleşmenin ödeme dönemi geçersiz.");
                var status = await db.Set<CollectionContractStatus>().AsNoTracking()
                    .SingleOrDefaultAsync(x => x.Id == contract.ContractStatusId, cancellationToken);
                var behavior = current.BillingBehavior == CollectionBillingBehavior.Free ? CollectionBillingBehavior.Free
                    : command.Freeze || !CollectionStartRules.IsIncluded(status?.Code) ? CollectionBillingBehavior.Suspended : CollectionBillingBehavior.Billable;
                if (behavior == CollectionBillingBehavior.Billable && (current.Amount is null || current.CurrencyTypeId is null))
                    return Fail("Aktifleştirme için dönem tutarı ve para birimi tanımlı olmalıdır.");
                next = new CollectionContractRatePeriod
                {
                    ContractId = id,
                    EffectiveFrom = today,
                    EffectiveToExclusive = current.EffectiveToExclusive,
                    BillingAnchor = command.Freeze ? current.BillingAnchor : today,
                    OriginalAnchorDay = command.Freeze ? current.OriginalAnchorDay : (byte)today.Day,
                    PaymentFrequencyId = current.PaymentFrequencyId,
                    Amount = current.Amount,
                    CurrencyTypeId = current.CurrencyTypeId,
                    BillingBehavior = behavior,
                    ChangeReason = (command.Freeze ? "Dondurma: " : "Aktifleştirme: ") + command.Reason.Trim(),
                    CreatedUser = actorId,
                    CreatedDate = now
                };
                current.EffectiveToExclusive = today;
                current.UpdatedUser = actorId;
                current.UpdatedDate = now;
                contract.SubscriptionStatusId = target.Id;
                contract.UpdatedUser = actorId;
                contract.UpdatedDate = now;
                var repository = new Repository(db);
                // Close first: the unique open-period index must be released before inserting the successor.
                await repository.CompleteAsync(cancellationToken);
                repository.Add(next);
                await repository.CompleteAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
                return ResponseModel<CollectionSubscriptionChanged>.Success(new(id, today), command.Freeze
                    ? "Abonelik donduruldu. Bugünden itibaren yeni borç oluşmaz."
                    : "Abonelik aktifleştirildi. Yeni dönem bugün başlar; donuk dönem için borç oluşturulmaz.");
            }
            catch (DbUpdateConcurrencyException)
            {
                return Fail("Sözleşme başka bir işlemde değiştirildi. Detayı yenileyip tekrar deneyin.", StatusCode.Conflict);
            }
            catch (DbUpdateException)
            {
                return Fail("İşlem tamamlanamadı. Detayı yenileyip güncel durumu kontrol edin ve tekrar deneyin.", StatusCode.Conflict);
            }
            catch (SqlException)
            {
                return Fail("İşlem sonucu doğrulanamadı. Detayı yenileyip güncel durumu kontrol edin.", StatusCode.Conflict);
            }
            catch (InvalidOperationException ex) when (ex.GetBaseException() is SqlException)
            {
                return Fail("Eşzamanlı işlem nedeniyle kayıt tamamlanamadı. Detayı yenileyip tekrar deneyin.", StatusCode.Conflict);
            }
            catch (TimeoutException)
            {
                return Fail("İşlem yanıtı alınamadı. Tekrar işlem yapmadan önce detayı yenileyip güncel durumu kontrol edin.", StatusCode.Conflict);
            }
            finally
            {
                if (next is not null) db.Entry(next).State = EntityState.Detached;
                // Queries may track two candidate rates before rejecting an inconsistent timeline.
                foreach (var entry in db.ChangeTracker.Entries<CollectionContractRatePeriod>().ToArray()) entry.State = EntityState.Detached;
                if (contract is not null) db.Entry(contract).State = EntityState.Detached;
            }
        });
    }
    private static ResponseModel<CollectionSubscriptionChanged> Fail(string message, StatusCode status = StatusCode.BadRequest) =>
        ResponseModel<CollectionSubscriptionChanged>.Fail(message, status);
}
