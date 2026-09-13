using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Business.Interfaces;
using Business.Services.Crm.Collections.Calculation;
using Core.Common;
using Core.Enums;
using Data.Concrete;
using Data.Concrete.EfCore.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Model.Concrete.Collections;
using Model.Dtos.Crm.Collections;

namespace Business.Services.Crm.Collections;

/// <summary>Menu authorization is enforced at the API boundary. Writes use one clean context and transaction.</summary>
public sealed class CollectionContractCreateService(AppDataContext db) : ICollectionContractCreateService
{
    public async Task<ResponseModel<CollectionContractCreated>> CreateAsync(CollectionContractCreate command, long actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        if (actorId <= 0) return Fail("Geçerli kullanıcı gereklidir.", StatusCode.Unauthorized);
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(command, new ValidationContext(command), errors, true))
            return Fail(string.Join(" ", errors.Select(x => x.ErrorMessage)));
        if (db.Database.CurrentTransaction is not null || db.ChangeTracker.Entries().Any())
            return Fail("Sözleşme kaydı için temiz işlem kapsamı gereklidir.", StatusCode.Conflict);
        var hash = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            Version = 1, command.CustomerId, command.ServiceTypeId,
            Start = command.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            End = command.EndDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            command.ContractStatusId, command.SubscriptionStatusId, command.PaymentFrequencyId, command.CurrencyTypeId,
            Amount = command.Amount.ToString("G29", CultureInfo.InvariantCulture), command.IsFree, command.GtsNo, command.IvrNo
        }));
        // Global context registration has no retries. Keep bounded SQL transient retries local to this idempotent command.
        var strategy = new ContractExecutionStrategy(db);
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
            CollectionContract? contract = null;
            CollectionContractRatePeriod? rate = null;
            try
            {
                var previous = await db.Set<CollectionContract>().AsNoTracking().SingleOrDefaultAsync(x => x.CreationRequestId == command.RequestId, cancellationToken);
                if (previous is not null)
                    return previous.CreatedUser == actorId && previous.CreationPayloadHash is not null
                        && CryptographicOperations.FixedTimeEquals(previous.CreationPayloadHash, hash)
                        ? ResponseModel<CollectionContractCreated>.Success(new(previous.Id, true), "Sözleşme daha önce oluşturuldu.")
                        : Fail("İşlem anahtarı farklı kullanıcı veya içerikle kullanılmış.", StatusCode.Conflict);
                if (!await db.Users.AsNoTracking().AnyAsync(x => x.Id == actorId && !x.IsDeleted, cancellationToken))
                    return Fail("Geçerli kullanıcı bulunamadı.", StatusCode.Unauthorized);
                if (!await db.Customers.AsNoTracking().AnyAsync(x => x.Id == command.CustomerId && !x.IsDeleted, cancellationToken))
                    return Fail("Geçerli müşteri bulunamadı.");
                if (!await db.ServiceTypes.AsNoTracking().AnyAsync(x => x.Id == command.ServiceTypeId && !x.IsDeleted, cancellationToken))
                    return Fail("Geçerli servis tipi bulunamadı.");
                if (!await db.CurrencyTypes.AsNoTracking().AnyAsync(x => x.Id == command.CurrencyTypeId, cancellationToken))
                    return Fail("Para birimi bulunamadı.");
                var frequency = await db.Set<CollectionPaymentFrequency>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == command.PaymentFrequencyId && x.IsActive, cancellationToken);
                var subscription = await db.Set<CollectionSubscriptionStatus>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == command.SubscriptionStatusId && x.IsActive, cancellationToken);
                var status = command.ContractStatusId is null ? null : await db.Set<CollectionContractStatus>().AsNoTracking()
                    .SingleOrDefaultAsync(x => x.Id == command.ContractStatusId && x.IsActive, cancellationToken);
                if (frequency is null || !CollectionPeriodRules.IsSupportedInterval(frequency.IntervalMonths)) return Fail("Ödeme dönemi geçersiz.");
                if (subscription is null || subscription.Code is not ("ACTIVE" or "FROZEN")) return Fail("Abonelik durumu geçersiz.");
                if (command.ContractStatusId.HasValue && (status is null || status.Code is not ("EXISTS" or "NONE" or "UNKNOWN"))) return Fail("Sözleşme durumu geçersiz.");
                var first = CollectionStartRules.FirstDueDate(command.StartDate);
                var end = CollectionPeriodRules.ToExclusiveEnd(command.EndDate);
                // Preserve signature on the contract independently from the initial billing phase.
                contract = new CollectionContract
                {
                    CreationRequestId = command.RequestId, CreationPayloadHash = hash,
                    CustomerId = command.CustomerId, ServiceTypeId = command.ServiceTypeId,
                    StartDate = command.StartDate, EndDate = command.EndDate, ContractStatusId = command.ContractStatusId,
                    SubscriptionStatusId = command.SubscriptionStatusId, GtsNo = command.GtsNo, IvrNo = command.IvrNo,
                    CreatedUser = actorId, CreatedDate = DateTimeOffset.UtcNow
                };
                var repository = new Repository(db);
                repository.Add(contract);
                await repository.CompleteAsync(cancellationToken);
                var behavior = command.IsFree ? CollectionBillingBehavior.Free
                    : !CollectionStartRules.IsIncluded(status?.Code) || subscription.Code == "FROZEN"
                        ? CollectionBillingBehavior.Suspended : CollectionBillingBehavior.Billable;
                rate = new CollectionContractRatePeriod
                {
                    ContractId = contract.Id, EffectiveFrom = command.StartDate, EffectiveToExclusive = end,
                    BillingAnchor = command.StartDate, OriginalAnchorDay = (byte)command.StartDate.Day,
                    PaymentFrequencyId = frequency.Id, Amount = command.Amount, CurrencyTypeId = command.CurrencyTypeId,
                    BillingBehavior = behavior, ChangeReason = "İlk sözleşme", CreatedUser = actorId, CreatedDate = contract.CreatedDate
                };
                // Deferred initial anchor must be persisted without rewriting actual signature date.
                if (first > command.StartDate)
                {
                    if (end <= first)
                    {
                        rate.BillingBehavior = CollectionBillingBehavior.Suspended;
                        rate.ChangeReason = "İlk borç tarihinden önce sona erdi";
                    }
                    else
                    {
                        rate.EffectiveFrom = first;
                        rate.BillingAnchor = first;
                    }
                }
                repository.Add(rate);
                await repository.CompleteAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
                return ResponseModel<CollectionContractCreated>.Success(new(contract.Id, false), "Sözleşme ve ilk tarife başarıyla oluşturuldu.");
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
            {
                return Fail("İşlem eşzamanlı kaydedilmiş olabilir. Aynı işlem anahtarıyla tekrar deneyin.", StatusCode.Conflict);
            }
            finally
            {
                if (rate is not null) db.Entry(rate).State = EntityState.Detached;
                if (contract is not null) db.Entry(contract).State = EntityState.Detached;
            }
        });
    }
    private static ResponseModel<CollectionContractCreated> Fail(string message, StatusCode status = StatusCode.BadRequest) =>
        ResponseModel<CollectionContractCreated>.Fail(message, status);

    private sealed class ContractExecutionStrategy(AppDataContext context)
        : SqlServerRetryingExecutionStrategy(context, 5, TimeSpan.FromSeconds(2), null)
    {
        protected override bool ShouldRetryOn(Exception exception)
        {
            // The application's non-retrying provider can wrap a transient SQL failure in InvalidOperationException.
            // Delegate only the underlying failure to EF's existing transient SQL classifier.
            while (exception is InvalidOperationException or DbUpdateException && exception.InnerException is not null)
                exception = exception.InnerException;
            return base.ShouldRetryOn(exception);
        }
    }
}
