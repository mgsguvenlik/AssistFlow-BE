using Business.Interfaces;
using Business.Services.Crm.Collections.Calculation;
using Business.UnitOfWork;
using Core.Common;
using Core.Enums;
using Data.Concrete.EfCore.Collections;
using Data.Concrete.EfCore.Context;
using Microsoft.EntityFrameworkCore;
using Model.Concrete.Collections;
using Model.Dtos.Crm.Collections;
using System.ComponentModel.DataAnnotations;

namespace Business.Services.Crm.Collections;

/// <summary>Read service; refuses database queries until the collection model is registered.</summary>
public sealed class CollectionContractReadService(AppDataContext db, IUnitOfWork unitOfWork) : ICollectionContractReadService
{
    public async Task<ResponseModel<CollectionPeriodBalance>> GetBalanceAsync(long id,
        CollectionBalanceQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow,
            "Turkey Standard Time").DateTime);
        var monthEnd = query.Period != default && query.Period.Year < 9999
            ? query.Period.AddMonths(1).AddDays(-1) : DateOnly.MaxValue;
        var asOf = query.AsOfDate ?? (today < monthEnd ? today : monthEnd);
        if (id <= 0 || query.Period == default || query.Period.Day != 1 || query.Period.Year >= 9999
            || asOf < query.Period || asOf > monthEnd || asOf > today)
            return ResponseModel<CollectionPeriodBalance>.Fail("Geçerli muhasebe ayı ve bu ay içinde, en fazla bugün olan hesap tarihi seçin.");
        if (db.Model.FindEntityType(typeof(CollectionContractRatePeriod)) is null)
            return ResponseModel<CollectionPeriodBalance>.Fail("Tahsilat modeli henüz etkin değil.", (StatusCode)503);
        var repository = unitOfWork.Repository;
        var contract = await repository.GetQueryable<CollectionContract>().AsNoTracking()
            .Where(x => x.Id == id && !x.IsDeleted).Select(x => new { x.EndDate }).SingleOrDefaultAsync(cancellationToken);
        if (contract is null)
            return ResponseModel<CollectionPeriodBalance>.Fail("Sözleşme bulunamadı.", StatusCode.NotFound);

        // Validate the complete timeline, not only an intersecting fragment that could hide a gap.
        // Bound a single-contract read; never load every customer's history into memory.
        var rates = await repository.GetQueryable<CollectionContractRatePeriod>().AsNoTracking()
            .Where(x => x.ContractId == id && !x.IsDeleted).OrderBy(x => x.EffectiveFrom).ThenBy(x => x.Id)
            .Select(x => new
            {
                x.Id, x.EffectiveFrom, x.EffectiveToExclusive, x.BillingAnchor, x.OriginalAnchorDay,
                x.Amount, x.CurrencyTypeId, x.BillingBehavior, x.PaymentFrequency.IntervalMonths,
                CurrencyCode = x.CurrencyType == null ? null : x.CurrencyType.Code
            }).Take(1001).ToListAsync(cancellationToken);
        if (rates.Count > 1000)
            return ResponseModel<CollectionPeriodBalance>.Fail("Sözleşmenin tarife geçmişi hesaplama sınırını aşıyor. Toplu takip sorgusunda incelenmelidir.", StatusCode.Conflict);
        DateOnly? contractEndExclusive;
        try { contractEndExclusive = CollectionPeriodRules.ToExclusiveEnd(contract.EndDate); }
        catch (ArgumentOutOfRangeException)
        {
            return ResponseModel<CollectionPeriodBalance>.Fail("Sözleşme bitiş tarihi bakiye hesabına uygun değil.", StatusCode.Conflict);
        }
        var calculationRates = rates.Where(x => !contractEndExclusive.HasValue || x.EffectiveFrom < contractEndExclusive.Value)
            .Select(x => new CollectionRateSegment(x.Id, x.EffectiveFrom,
                contractEndExclusive.HasValue && (!x.EffectiveToExclusive.HasValue || contractEndExclusive < x.EffectiveToExclusive)
                    ? contractEndExclusive : x.EffectiveToExclusive,
                x.BillingAnchor, x.IntervalMonths, x.Amount, x.CurrencyTypeId,
                x.BillingBehavior, x.OriginalAnchorDay)).ToArray();
        var firstRate = calculationRates.Length == 0 ? query.Period
            : calculationRates.Min(x => x.EffectiveFrom);
        var firstRatePeriod = new DateOnly(firstRate.Year, firstRate.Month, 1);
        var from = query.IncludeCarryOver && firstRatePeriod < query.Period
            ? firstRatePeriod : query.Period;
        var until = query.Period.AddMonths(1);
        if (asOf < until) until = asOf.AddDays(1);
        IReadOnlyList<CollectionCharge> charges;
        try
        {
            charges = CollectionAccrualRules.Calculate(calculationRates, from, until);
        }
        catch (ArgumentException)
        {
            return ResponseModel<CollectionPeriodBalance>.Fail("Dönem bakiyesi hesaplanamadı. Tarife geçmişindeki eksik bilgi, tarih aralığı, ödeme sıklığı veya para birimi kontrol edilmelidir.", StatusCode.Conflict);
        }

        var payments = await repository.GetQueryable<CollectionPayment>().AsNoTracking()
            .Where(x => x.ContractId == id && x.Period >= from && x.Period <= query.Period && x.PaymentDate <= asOf)
            .GroupBy(x => new { x.CurrencyTypeId, x.CurrencyType.Code })
            .Select(g => new { g.Key.CurrencyTypeId, g.Key.Code, Amount = g.Sum(x => x.Amount) })
            .OrderBy(x => x.CurrencyTypeId).Take(101).ToListAsync(cancellationToken);
        if (payments.Count > 100)
            return ResponseModel<CollectionPeriodBalance>.Fail("Dönemdeki para birimi sayısı desteklenen sınırı aşıyor.", StatusCode.Conflict);
        var accrued = charges.GroupBy(x => x.CurrencyTypeId).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
        var paid = payments.ToDictionary(x => x.CurrencyTypeId, x => x.Amount);
        var currencyCodes = rates.Where(x => x.CurrencyTypeId.HasValue).GroupBy(x => x.CurrencyTypeId!.Value)
            .ToDictionary(g => g.Key, g => g.First().CurrencyCode ?? string.Empty);
        foreach (var payment in payments) currencyCodes[payment.CurrencyTypeId] = payment.Code;
        var items = accrued.Keys.Union(paid.Keys).OrderBy(x => x).Select(currencyId =>
            new CollectionCurrencyBalance(currencyId, currencyCodes[currencyId], accrued.GetValueOrDefault(currencyId),
                paid.GetValueOrDefault(currencyId), accrued.GetValueOrDefault(currencyId) - paid.GetValueOrDefault(currencyId))).ToList();
        return ResponseModel<CollectionPeriodBalance>.Success(new(query.Period, from, asOf,
            query.IncludeCarryOver, items), query.IncludeCarryOver ? "Devirli bakiye hesaplandı." : "Dönem bakiyesi hesaplandı.");
    }

    public async Task<ResponseModel<PagedResult<CollectionPaymentItem>>> GetPaymentsAsync(long id,
        CollectionPaymentQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        var errors = new List<ValidationResult>();
        if (id <= 0 || !Validator.TryValidateObject(query, new ValidationContext(query), errors, true))
            return ResponseModel<PagedResult<CollectionPaymentItem>>.Fail("Geçersiz sözleşme veya sayfa bilgisi.");
        if (db.Model.FindEntityType(typeof(CollectionPayment)) is null)
            return ResponseModel<PagedResult<CollectionPaymentItem>>.Fail("Tahsilat ödeme modeli henüz etkin değil.", (StatusCode)503);
        var repository = unitOfWork.Repository;
        if (!await repository.GetQueryable<CollectionContract>().AsNoTracking()
            .AnyAsync(x => x.Id == id && !x.IsDeleted, cancellationToken))
            return ResponseModel<PagedResult<CollectionPaymentItem>>.Fail("Sözleşme bulunamadı.", StatusCode.NotFound);
        var source = repository.GetQueryable<CollectionPayment>().AsNoTracking().Where(x => x.ContractId == id);
        var count = await source.CountAsync(cancellationToken);
        var rows = await source.OrderByDescending(x => x.PaymentDate).ThenByDescending(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new CollectionPaymentItem
            {
                Id = x.Id, Period = x.Period, PaymentDate = x.PaymentDate, Amount = x.Amount,
                CurrencyCode = x.CurrencyType.Code, CurrencyTypeId = x.CurrencyTypeId,
                Description = x.Description, IsFree = x.IsFree, RowVersion = x.RowVersion
            }).ToListAsync(cancellationToken);
        return ResponseModel<PagedResult<CollectionPaymentItem>>.Success(new(rows, count, query.Page, query.PageSize),
            "Ödeme hareketleri başarıyla getirildi.");
    }
    public async Task<ResponseModel<PagedResult<CollectionContractListItem>>> GetPageAsync(
        CollectionContractQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        if (db.Model.FindEntityType(typeof(CollectionContract)) is null)
            return ResponseModel<PagedResult<CollectionContractListItem>>.Fail(
                "Tahsilat sözleşme modeli henüz etkin değil.", (StatusCode)503);
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(query, new ValidationContext(query), errors, true))
            return ResponseModel<PagedResult<CollectionContractListItem>>.Fail(
                string.Join(" ", errors.Select(x => x.ErrorMessage)), StatusCode.BadRequest);

        var source = unitOfWork.Repository.GetQueryable<CollectionContract>();
        // Sequential queries: DbContext does not support parallel operations.
        // Concurrent changes between count and page are possible; no snapshot guarantee is implied.
        var count = await CollectionContractReadQuery.Filter(source, query).CountAsync(cancellationToken);
        var items = await CollectionContractReadQuery.Page(source, query).ToListAsync(cancellationToken);
        return ResponseModel<PagedResult<CollectionContractListItem>>.Success(new(items, count, query.Page, query.PageSize), "Sözleşmeler başarıyla getirildi.");
    }

    public async Task<ResponseModel<CollectionContractDetail>> GetDetailAsync(long id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (db.Model.FindEntityType(typeof(CollectionContract)) is null)
            return ResponseModel<CollectionContractDetail>.Fail(
                "Tahsilat sözleşme modeli henüz etkin değil.", (StatusCode)503);
        if (id <= 0) return ResponseModel<CollectionContractDetail>.Fail("Geçersiz sözleşme kimliği.");
        var item = await CollectionContractReadQuery.Detail(unitOfWork.Repository.GetQueryable<CollectionContract>(), id)
            .SingleOrDefaultAsync(cancellationToken);
        return item is null
            ? ResponseModel<CollectionContractDetail>.Fail("Sözleşme bulunamadı.", StatusCode.NotFound)
            : ResponseModel<CollectionContractDetail>.Success(item, "Sözleşme bilgileri başarıyla getirildi.");
    }

    public async Task<ResponseModel<PagedResult<CollectionRateHistoryItem>>> GetHistoryAsync(long id,
        CollectionRateHistoryQuery query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (db.Model.FindEntityType(typeof(CollectionContractRatePeriod)) is null)
            return ResponseModel<PagedResult<CollectionRateHistoryItem>>.Fail("Tahsilat tarife modeli henüz etkin değil.", (StatusCode)503);
        var errors = new List<ValidationResult>();
        if (id <= 0 || !Validator.TryValidateObject(query, new ValidationContext(query), errors, true))
            return ResponseModel<PagedResult<CollectionRateHistoryItem>>.Fail("Geçersiz sözleşme veya sayfa bilgisi.");
        var repository = unitOfWork.Repository;
        if (!await repository.GetQueryable<CollectionContract>().AsNoTracking()
            .AnyAsync(x => x.Id == id && !x.IsDeleted, cancellationToken))
            return ResponseModel<PagedResult<CollectionRateHistoryItem>>.Fail("Sözleşme bulunamadı.", StatusCode.NotFound);
        var source = repository.GetQueryable<CollectionContractRatePeriod>();
        var count = await source.CountAsync(x => x.ContractId == id && !x.IsDeleted, cancellationToken);
        var items = await CollectionContractReadQuery.History(source, id, query).ToListAsync(cancellationToken);
        return ResponseModel<PagedResult<CollectionRateHistoryItem>>.Success(new(items, count, query.Page, query.PageSize),
            "Tarife geçmişi başarıyla getirildi.");
    }
}
