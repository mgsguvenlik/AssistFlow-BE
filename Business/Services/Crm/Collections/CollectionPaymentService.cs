using Business.Interfaces;
using Core.Common;
using Core.Enums;
using Data.Concrete.EfCore.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Model.Concrete.Collections;
using Model.Dtos.Crm.Collections;
using System.Security.Cryptography;
using System.Text;

namespace Business.Services.Crm.Collections;

public sealed class CollectionPaymentService(AppDataContext db) : ICollectionPaymentService
{
    public async Task<ResponseModel<CollectionPaymentBatchResult>> CreateBatchAsync(CollectionPaymentBatchCreate command,
        long actorId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.RequestId == Guid.Empty || command.PaymentDate == default || command.Items is null
            || command.Items.Count is < 1 or > 50)
            return BatchFail("Geçerli işlem anahtarı, ödeme tarihi ve 1-50 arası kayıt gereklidir.");
        if (command.Items.Any(x => x.ContractId <= 0 || x.CurrencyTypeId <= 0 || x.Period == default
                || x.Period.Day != 1 || x.Period.Year >= 9999 || x.ExpectedRemainingAmount <= 0
                || x.ExpectedRemainingAmount > 9999999999999999.99m
                || decimal.Round(x.ExpectedRemainingAmount, 2) != x.ExpectedRemainingAmount)
            || command.Items.GroupBy(x => new { x.ContractId, x.Period, x.CurrencyTypeId }).Any(x => x.Count() > 1))
            return BatchFail("Seçilen tahsilat kayıtlarından biri geçersiz veya tekrarlıdır.");
        try
        {
            if (!await ValidActorAsync(actorId, cancellationToken))
                return BatchFail("Geçerli kullanıcı bulunamadı.", StatusCode.Unauthorized);
            var completed = 0;
            var replayed = 0;
            var failures = new List<CollectionPaymentBatchFailure>();
            foreach (var item in command.Items)
            {
                var childRequestId = CreateChildRequestId(command.RequestId, item);
                var payload = new CollectionPaymentCommand(CollectionPaymentOperationKind.Create, null, null,
                    item.ContractId, item.Period, command.PaymentDate, item.ExpectedRemainingAmount,
                    item.CurrencyTypeId, command.Description, false);
                var result = await new CollectionPaymentTransaction(db).ExecuteAsync(childRequestId, actorId, payload,
                    cancellationToken, token => ValidateCurrentRemainingAsync(item, token));
                if (result.IsSuccess && result.Data is not null)
                {
                    completed++;
                    if (result.Data.Replayed) replayed++;
                }
                else
                {
                    failures.Add(new(item.ContractId, result.Message ?? "Tahsilat kaydedilemedi."));
                    if ((int)result.StatusCode >= 500)
                        return ResponseModel<CollectionPaymentBatchResult>.Fail(
                            "Toplu tahsilatın bir bölümü doğrulanamadı. Aynı işlem anahtarıyla tekrar deneyin.",
                            (StatusCode)503, new(completed, replayed, failures));
                }
            }
            var data = new CollectionPaymentBatchResult(completed, replayed, failures);
            var message = failures.Count == 0
                ? $"{completed} tahsilat başarıyla kaydedildi."
                : $"{completed} tahsilat kaydedildi, {failures.Count} kayıt güncel olmadığı için atlandı.";
            return ResponseModel<CollectionPaymentBatchResult>.Success(data, message);
        }
        catch (Exception ex) when (IsStorageFailure(ex))
        {
            return ResponseModel<CollectionPaymentBatchResult>.Fail(
                "Toplu tahsilat sonucu doğrulanamadı. Aynı işlem anahtarıyla tekrar deneyin; çift kayıt oluşturulmaz.",
                (StatusCode)503);
        }
    }

    public async Task<ResponseModel<CollectionPaymentCommitResult>> CreateAsync(long contractId,
        CollectionPaymentCreate command, long actorId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (contractId <= 0 || command.RequestId == Guid.Empty)
            return Fail("Geçerli sözleşme ve işlem anahtarı gereklidir.");
        if (command.Amount <= 0)
            return Fail("Tahsilat tutarı sıfırdan büyük olmalıdır.");
        if (command.Period == default || command.Period.Day != 1 || command.Period.Year == 9999
            || command.PaymentDate == default)
            return Fail("Geçerli ödeme tarihi ve muhasebe dönemi gereklidir.");
        try
        {
            if (actorId <= 0 || !await db.Users.AsNoTracking()
                .AnyAsync(x => x.Id == actorId && !x.IsDeleted, cancellationToken))
                return Fail("Geçerli kullanıcı bulunamadı.", StatusCode.Unauthorized);
            var payload = new CollectionPaymentCommand(CollectionPaymentOperationKind.Create, null, null,
                contractId, command.Period, command.PaymentDate, command.Amount, command.CurrencyTypeId,
                command.Description, false);
            return await new CollectionPaymentTransaction(db).ExecuteAsync(command.RequestId, actorId, payload,
                cancellationToken, async token =>
                {
                    if (!await db.Set<CollectionContract>().AsNoTracking().AnyAsync(
                        x => x.Id == contractId && !x.IsDeleted && !x.Customer.IsDeleted, token))
                        return "Tahsilata uygun sözleşme veya müşteri bulunamadı.";
                    var until = command.Period.AddMonths(1);
                    // Historical periods remain payable even when the subscriber is currently frozen/free.
                    // Do not reinterpret historical rates using the current subscription status.
                    if (!await db.Set<CollectionContractRatePeriod>().AsNoTracking().AnyAsync(x =>
                        x.ContractId == contractId && !x.IsDeleted && x.EffectiveFrom < until
                        && (x.EffectiveToExclusive == null || x.EffectiveToExclusive > command.Period)
                        && x.CurrencyTypeId == command.CurrencyTypeId
                        && x.BillingBehavior == CollectionBillingBehavior.Billable, token))
                        return "Seçilen dönem ve para biriminde ücretli tarife bulunamadı. Dönemi ve para birimini kontrol edin.";
                    return null;
                });
        }
        catch (Exception ex) when (ex is DbUpdateException or SqlException or TimeoutException
            || ex is InvalidOperationException && ex.GetBaseException() is SqlException)
        {
            return Uncertain();
        }
    }

    public async Task<ResponseModel<CollectionPaymentCommitResult>> UpdateAsync(long contractId, long paymentId,
        CollectionPaymentUpdate command, long actorId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (contractId <= 0 || paymentId <= 0 || command.RequestId == Guid.Empty)
            return Fail("Geçerli sözleşme, ödeme ve işlem anahtarı gereklidir.");
        if (!ValidTerms(command.Period, command.PaymentDate, command.Amount))
            return Fail("Geçerli dönem, ödeme tarihi ve sıfırdan büyük tutar gereklidir.");
        try
        {
            if (!await ValidActorAsync(actorId, cancellationToken))
                return Fail("Geçerli kullanıcı bulunamadı.", StatusCode.Unauthorized);
            var payload = new CollectionPaymentCommand(CollectionPaymentOperationKind.Update, paymentId,
                command.RowVersion, contractId, command.Period, command.PaymentDate, command.Amount,
                command.CurrencyTypeId, command.Description, false);
            return await new CollectionPaymentTransaction(db).ExecuteAsync(command.RequestId, actorId, payload,
                cancellationToken, token => ValidateMutationAsync(contractId, paymentId, command.Period,
                    command.CurrencyTypeId, token));
        }
        catch (Exception ex) when (IsStorageFailure(ex))
        {
            return Uncertain();
        }
    }

    public async Task<ResponseModel<CollectionPaymentCommitResult>> DeleteAsync(long contractId, long paymentId,
        CollectionPaymentDelete command, long actorId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (contractId <= 0 || paymentId <= 0 || command.RequestId == Guid.Empty)
            return Fail("Geçerli sözleşme, ödeme ve işlem anahtarı gereklidir.");
        try
        {
            if (!await ValidActorAsync(actorId, cancellationToken))
                return Fail("Geçerli kullanıcı bulunamadı.", StatusCode.Unauthorized);
            var payload = new CollectionPaymentCommand(CollectionPaymentOperationKind.Delete, paymentId,
                command.RowVersion, null, null, null, null, null, null, null);
            return await new CollectionPaymentTransaction(db).ExecuteAsync(command.RequestId, actorId, payload,
                cancellationToken, async token => await db.Set<CollectionPayment>().AsNoTracking()
                    .AnyAsync(x => x.Id == paymentId && x.ContractId == contractId, token)
                        ? null : "Ödeme kaydı bu sözleşmede bulunamadı.");
        }
        catch (Exception ex) when (IsStorageFailure(ex))
        {
            return Uncertain();
        }
    }

    private async Task<string?> ValidateMutationAsync(long contractId, long paymentId, DateOnly period,
        long currencyTypeId, CancellationToken token)
    {
        if (!await db.Set<CollectionPayment>().AsNoTracking()
            .AnyAsync(x => x.Id == paymentId && x.ContractId == contractId, token))
            return "Ödeme kaydı bu sözleşmede bulunamadı.";
        if (!await db.Set<CollectionContract>().AsNoTracking()
            .AnyAsync(x => x.Id == contractId && !x.IsDeleted && !x.Customer.IsDeleted, token))
            return "Tahsilata uygun sözleşme veya müşteri bulunamadı.";
        var until = period.AddMonths(1);
        return await db.Set<CollectionContractRatePeriod>().AsNoTracking().AnyAsync(x =>
            x.ContractId == contractId && !x.IsDeleted && x.EffectiveFrom < until
            && (x.EffectiveToExclusive == null || x.EffectiveToExclusive > period)
            && x.CurrencyTypeId == currencyTypeId
            && x.BillingBehavior == CollectionBillingBehavior.Billable, token)
                ? null : "Seçilen dönem ve para biriminde ücretli tarife bulunamadı. Dönemi ve para birimini kontrol edin.";
    }

    private Task<bool> ValidActorAsync(long actorId, CancellationToken token) => actorId > 0
        ? db.Users.AsNoTracking().AnyAsync(x => x.Id == actorId && !x.IsDeleted, token)
        : Task.FromResult(false);

    private async Task<string?> ValidateCurrentRemainingAsync(CollectionPaymentBatchItem item, CancellationToken token)
    {
        var contract = await db.Set<CollectionContract>().AsNoTracking()
            .Where(x => x.Id == item.ContractId && !x.IsDeleted && !x.Customer.IsDeleted)
            .Select(x => new { x.EndDate }).SingleOrDefaultAsync(token);
        if (contract is null) return "Tahsilata uygun sözleşme veya müşteri bulunamadı.";

        var monthDays = DateTime.DaysInMonth(item.Period.Year, item.Period.Month);
        var rates = await db.Set<CollectionContractRatePeriod>().AsNoTracking()
            .Where(x => x.ContractId == item.ContractId && !x.IsDeleted
                && x.BillingBehavior == CollectionBillingBehavior.Billable && x.Amount != null
                && x.CurrencyTypeId == item.CurrencyTypeId)
            .Select(x => new { x.Amount, x.EffectiveFrom, x.EffectiveToExclusive, x.BillingAnchor,
                x.OriginalAnchorDay, IntervalMonths = x.PaymentFrequency.IntervalMonths })
            .ToListAsync(token);
        decimal accrued = 0;
        foreach (var rate in rates)
        {
            var months = (item.Period.Year - rate.BillingAnchor.Year) * 12 + item.Period.Month - rate.BillingAnchor.Month;
            if (months < 0 || months % rate.IntervalMonths != 0) continue;
            var dueDay = Math.Min(rate.OriginalAnchorDay ?? (byte)rate.BillingAnchor.Day, (byte)monthDays);
            var dueDate = item.Period.AddDays(dueDay - 1);
            if (rate.EffectiveFrom <= dueDate && (rate.EffectiveToExclusive is null || rate.EffectiveToExclusive > dueDate)
                && (contract.EndDate is null || contract.EndDate >= dueDate))
                accrued += rate.Amount!.Value;
        }
        var paid = await db.Set<CollectionPayment>().AsNoTracking()
            .Where(x => x.ContractId == item.ContractId && x.Period == item.Period
                && x.CurrencyTypeId == item.CurrencyTypeId).SumAsync(x => (decimal?)x.Amount, token) ?? 0;
        var remaining = decimal.Round(accrued - paid, 2);
        return remaining > 0 && remaining == item.ExpectedRemainingAmount
            ? null
            : "Kalan tutar değişti. Listeyi yenileyip kaydı tekrar seçin.";
    }

    private static Guid CreateChildRequestId(Guid batchRequestId, CollectionPaymentBatchItem item)
    {
        var value = $"{batchRequestId:N}|{item.ContractId}|{item.Period:yyyy-MM-dd}|{item.CurrencyTypeId}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(hash.AsSpan(0, 16));
    }

    private static bool ValidTerms(DateOnly period, DateOnly paymentDate, decimal amount) =>
        period != default && period.Day == 1 && period.Year < 9999 && paymentDate != default
        && amount > 0 && amount <= 9999999999999999.99m && decimal.Round(amount, 2) == amount;

    private static bool IsStorageFailure(Exception ex) => ex is DbUpdateException or SqlException or TimeoutException
        || ex is InvalidOperationException && ex.GetBaseException() is SqlException;

    private static ResponseModel<CollectionPaymentCommitResult> Uncertain() =>
        Fail("Tahsilat sonucu doğrulanamadı. Aynı bilgiler ve işlem anahtarıyla tekrar deneyin; çift kayıt oluşturulmaz.", (StatusCode)503);

    private static ResponseModel<CollectionPaymentCommitResult> Fail(string message, StatusCode status = StatusCode.BadRequest) =>
        ResponseModel<CollectionPaymentCommitResult>.Fail(message, status);

    private static ResponseModel<CollectionPaymentBatchResult> BatchFail(string message,
        StatusCode status = StatusCode.BadRequest) => ResponseModel<CollectionPaymentBatchResult>.Fail(message, status);
}
