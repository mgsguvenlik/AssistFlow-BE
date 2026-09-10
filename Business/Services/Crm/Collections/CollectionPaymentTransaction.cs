using System.Data;
using System.ComponentModel.DataAnnotations;
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

/// <summary>
/// Persistence primitive for an already authorized and financially validated command.
/// Not registered in DI or exposed by an endpoint. Eligibility checks belong to the command service.
/// </summary>
public sealed class CollectionPaymentTransaction(AppDataContext db)
{
    public async Task<ResponseModel<CollectionPaymentCommitResult>> ExecuteAsync(Guid requestId, long actorUserId,
        CollectionPaymentCommand command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (requestId == Guid.Empty || actorUserId <= 0)
            return Fail("Geçerli işlem anahtarı ve kullanıcı kimliği gereklidir.");
        byte[] hash;
        try { hash = CollectionPaymentCommandRules.ComputeHash(command); }
        catch (ValidationException exception) { return Fail(exception.Message); }
        if (db.Model.FindEntityType(typeof(CollectionPayment)) is null || db.Model.FindEntityType(typeof(CollectionPaymentOperation)) is null)
            return Fail("Tahsilat ödeme modeli henüz etkin değil.", (StatusCode)503);
        // A dedicated clean context prevents this operation from saving unrelated caller changes.
        if (db.Database.CurrentTransaction is not null || db.ChangeTracker.Entries().Any())
            return Fail("Ödeme işlemi için bağımsız ve temiz bir işlem kapsamı gereklidir.", StatusCode.Conflict);

        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            CollectionPayment? payment = null;
            CollectionPaymentOperation? receipt = null;
            try
            {
                var previous = await db.Set<CollectionPaymentOperation>().AsNoTracking()
                    .SingleOrDefaultAsync(x => x.RequestId == requestId, cancellationToken);
                if (previous is not null)
                {
                    if (!CollectionPaymentReplayRules.CanReplay(previous.ActorUserId, actorUserId, previous.PayloadHash, hash))
                        return Fail("İşlem anahtarı farklı bir kullanıcı veya içerikle kullanılmış.", StatusCode.Conflict);
                    return ResponseModel<CollectionPaymentCommitResult>.Success(
                        new(previous.PaymentId, previous.Kind, true), "İşlem daha önce tamamlanmış.");
                }

                var repository = new Repository(db); // Reuse repository, but bind every write to this transaction's context.
                string? before = null;
                if (command.Kind == CollectionPaymentOperationKind.Create)
                    payment = new CollectionPayment { CreatedDate = DateTimeOffset.UtcNow, CreatedUser = actorUserId };
                else
                {
                    payment = await repository.GetQueryable<CollectionPayment>()
                        .SingleOrDefaultAsync(x => x.Id == command.PaymentId, cancellationToken);
                    if (payment is null) return Fail("Ödeme kaydı bulunamadı.", StatusCode.NotFound);
                    var expected = Convert.FromBase64String(command.ExpectedRowVersion!);
                    if (!payment.RowVersion.SequenceEqual(expected))
                        return Fail("Ödeme kaydı değişmiş. Güncel bilgileri yükleyip tekrar deneyin.", StatusCode.Conflict);
                    db.Entry(payment).Property(x => x.RowVersion).OriginalValue = expected;
                    before = CollectionPaymentSnapshotRules.Serialize(payment);
                }

                if (command.Kind == CollectionPaymentOperationKind.Delete)
                    repository.HardDelete(payment);
                else
                {
                    // Referential checks here do not substitute for customer/period/amount eligibility checks.
                    if (!await repository.GetQueryable<CollectionContract>().AsNoTracking().AnyAsync(
                        x => x.Id == command.ContractId && !x.IsDeleted, cancellationToken))
                        return Fail("Geçerli sözleşme bulunamadı.");
                    if (!await repository.GetQueryable<Model.Concrete.CurrencyType>().AsNoTracking().AnyAsync(
                        x => x.Id == command.CurrencyTypeId, cancellationToken))
                        return Fail("Para birimi bulunamadı.");
                    payment.ContractId = command.ContractId!.Value;
                    payment.Period = command.Period!.Value;
                    payment.PaymentDate = command.PaymentDate!.Value;
                    payment.Amount = command.Amount!.Value;
                    payment.CurrencyTypeId = command.CurrencyTypeId!.Value;
                    payment.Description = command.Description;
                    payment.IsFree = command.IsFree!.Value;
                    if (command.Kind == CollectionPaymentOperationKind.Create) repository.Add(payment);
                    else { payment.UpdatedDate = DateTimeOffset.UtcNow; payment.UpdatedUser = actorUserId; }
                }

                await repository.CompleteAsync(cancellationToken);
                receipt = new CollectionPaymentOperation
                {
                    RequestId = requestId, ActorUserId = actorUserId, Kind = command.Kind,
                    PayloadHash = hash, PaymentId = payment.Id, BeforeJson = before,
                    AfterJson = command.Kind == CollectionPaymentOperationKind.Delete ? null : CollectionPaymentSnapshotRules.Serialize(payment),
                    CompletedDate = DateTimeOffset.UtcNow
                };
                repository.Add(receipt);
                await repository.CompleteAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return ResponseModel<CollectionPaymentCommitResult>.Success(
                    new(payment.Id, command.Kind, false), "Ödeme işlemi başarıyla tamamlandı.");
            }
            catch (DbUpdateConcurrencyException)
            {
                return Fail("Ödeme kaydı başka bir işlem tarafından değiştirilmiş.", StatusCode.Conflict);
            }
            catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
            {
                return Fail("İşlem anahtarı eşzamanlı kullanılmış. Aynı anahtarla tekrar deneyin.", StatusCode.Conflict);
            }
            finally
            {
                // Disposal rolls back any uncommitted transaction, including early validation returns.
                if (payment is not null) db.Entry(payment).State = EntityState.Detached;
                if (receipt is not null) db.Entry(receipt).State = EntityState.Detached;
            }
        });
    }

    private static ResponseModel<CollectionPaymentCommitResult> Fail(string message, StatusCode status = StatusCode.BadRequest) =>
        ResponseModel<CollectionPaymentCommitResult>.Fail(message, status);
}

/// <summary>A receipt of the operation, not a claim that the payment still exists or has unchanged values.</summary>
public sealed record CollectionPaymentCommitResult(long PaymentId, CollectionPaymentOperationKind Kind, bool Replayed);
