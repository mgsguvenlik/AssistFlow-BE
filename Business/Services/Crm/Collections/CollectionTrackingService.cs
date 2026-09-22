using System.ComponentModel.DataAnnotations;
using Business.Interfaces;
using Business.UnitOfWork;
using Core.Common;
using Core.Enums;
using Microsoft.EntityFrameworkCore;
using Model.Concrete.Collections;
using Model.Dtos.Crm.Collections;

namespace Business.Services.Crm.Collections;

public sealed class CollectionTrackingService(IUnitOfWork unitOfWork) : ICollectionTrackingService
{
    public async Task<ResponseModel<PagedResult<CollectionTrackingItem>>> GetPageAsync(CollectionTrackingQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var validationError = Validate(query);
        if (validationError is not null) return ResponseModel<PagedResult<CollectionTrackingItem>>.Fail(validationError);
        var rows = BuildRows(query);
        var count = await rows.CountAsync(cancellationToken);
        var items = await OrderRows(rows, query).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .ToListAsync(cancellationToken);
        return ResponseModel<PagedResult<CollectionTrackingItem>>.Success(new(items, count, query.Page, query.PageSize),
            "Tahsilat takip kayıtları getirildi.");
    }

    public ResponseModel<IAsyncEnumerable<CollectionTrackingItem>> GetExportRows(CollectionTrackingQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        var validationError = Validate(query);
        if (validationError is not null) return ResponseModel<IAsyncEnumerable<CollectionTrackingItem>>.Fail(validationError);
        return ResponseModel<IAsyncEnumerable<CollectionTrackingItem>>.Success(
            OrderRows(BuildRows(query), query).AsAsyncEnumerable());
    }

    private static string? Validate(CollectionTrackingQuery query)
    {
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(query, new ValidationContext(query), errors, true)
            || query.Period == default || query.Period.Day != 1 || query.Period.Year >= 9999
            || !Enum.IsDefined(query.SortBy) || !Enum.IsDefined(query.View) || !Enum.IsDefined(query.BalanceFilter))
            return errors.FirstOrDefault()?.ErrorMessage ?? "Geçerli muhasebe dönemi ve sıralama bilgisi gereklidir.";
        return null;
    }

    private IQueryable<CollectionTrackingItem> BuildRows(CollectionTrackingQuery query)
    {
        var repository = unitOfWork.Repository;
        var period = query.Period;
        var nextPeriod = period.AddMonths(1);
        var monthDays = DateTime.DaysInMonth(period.Year, period.Month);
        var payments = repository.GetQueryable<CollectionPayment>().AsNoTracking()
            .Where(x => x.Period == period)
            .GroupBy(x => new { x.ContractId, x.CurrencyTypeId })
            .Select(g => new { g.Key.ContractId, g.Key.CurrencyTypeId, Amount = (decimal?)g.Sum(x => x.Amount) });
        var rates = repository.GetQueryable<CollectionContractRatePeriod>().AsNoTracking()
            .Where(x => !x.IsDeleted && x.BillingBehavior == CollectionBillingBehavior.Billable
                && x.Amount != null && x.CurrencyTypeId != null && !x.Contract.IsDeleted && !x.Contract.Customer.IsDeleted
                && x.EffectiveFrom < nextPeriod && (x.EffectiveToExclusive == null || x.EffectiveToExclusive > period)
                && (x.Contract.EndDate == null || x.Contract.EndDate >= period));
        var candidates = rates.Select(x => new
        {
            Rate = x,
            MonthDifference = EF.Functions.DateDiffMonth(x.BillingAnchor, period),
            AnchorDay = x.OriginalAnchorDay ?? (byte)x.BillingAnchor.Day
        }).Where(x => x.MonthDifference >= 0 && x.MonthDifference % x.Rate.PaymentFrequency.IntervalMonths == 0)
            .Select(x => new
            {
                x.Rate,
                DueDay = x.AnchorDay > monthDays ? monthDays : x.AnchorDay
            }).Where(x =>
                (x.Rate.EffectiveFrom.Year < period.Year || x.Rate.EffectiveFrom.Year == period.Year
                    && (x.Rate.EffectiveFrom.Month < period.Month || x.Rate.EffectiveFrom.Month == period.Month && x.Rate.EffectiveFrom.Day <= x.DueDay))
                && (x.Rate.EffectiveToExclusive == null || x.Rate.EffectiveToExclusive.Value.Year > period.Year
                    || x.Rate.EffectiveToExclusive.Value.Year == period.Year && (x.Rate.EffectiveToExclusive.Value.Month > period.Month
                    || x.Rate.EffectiveToExclusive.Value.Month == period.Month && x.Rate.EffectiveToExclusive.Value.Day > x.DueDay))
                && (x.Rate.Contract.EndDate == null || x.Rate.Contract.EndDate.Value.Year > period.Year
                    || x.Rate.Contract.EndDate.Value.Year == period.Year && (x.Rate.Contract.EndDate.Value.Month > period.Month
                    || x.Rate.Contract.EndDate.Value.Month == period.Month && x.Rate.Contract.EndDate.Value.Day >= x.DueDay)));
        var contracts = repository.GetQueryable<CollectionContract>().AsNoTracking()
            .Where(x => !x.IsDeleted && !x.Customer.IsDeleted);
        var currencies = repository.GetQueryable<Model.Concrete.CurrencyType>().AsNoTracking();
        var dueCandidates = candidates.Select(x => new
        {
            x.Rate.ContractId,
            CurrencyTypeId = x.Rate.CurrencyTypeId!.Value,
            DueDay = (int?)x.DueDay,
            Amount = (decimal?)x.Rate.Amount!.Value
        });
        var chargeKeys = dueCandidates.Select(x => new { x.ContractId, x.CurrencyTypeId });
        var paymentKeys = payments.Select(x => new { x.ContractId, x.CurrencyTypeId });
        var keys = chargeKeys.Union(paymentKeys);
        IQueryable<CollectionTrackingItem> rows =
            from key in keys
            join contract in contracts on key.ContractId equals contract.Id
            join currency in currencies on key.CurrencyTypeId equals currency.Id
            join candidate in dueCandidates on key equals new
                { candidate.ContractId, candidate.CurrencyTypeId } into candidateJoin
            from candidate in candidateJoin.DefaultIfEmpty()
            join payment in payments on key equals new
                { payment.ContractId, payment.CurrencyTypeId } into paymentJoin
            from payment in paymentJoin.DefaultIfEmpty()
            select new CollectionTrackingItem
            {
                ContractId = contract.Id, CustomerId = contract.CustomerId,
                ServiceTypeId = contract.ServiceTypeId,
                CustomerGroupId = contract.Customer.CustomerGroupId,
                CustomerGroupName = contract.Customer.CustomerGroup == null
                    ? null : contract.Customer.CustomerGroup.GroupName,
                Period = period,
                DueDate = candidate.DueDay == null ? null : period.AddDays(candidate.DueDay.Value - 1),
                SubscriberCode = contract.Customer.SubscriberCode,
                CustomerName = contract.Customer.SubscriberCompany,
                ServiceTypeName = contract.ServiceType.Name,
                CurrencyTypeId = key.CurrencyTypeId, CurrencyCode = currency.Code,
                AccruedAmount = candidate.Amount ?? 0,
                PaymentAmount = payment.Amount ?? 0,
                RemainingAmount = (candidate.Amount ?? 0)
                    - (payment.Amount ?? 0),
                HasAccrual = candidate.DueDay != null, IsGroup = false, ContractCount = 1
            };
        var term = query.Search?.Trim();
        if (!string.IsNullOrEmpty(term)) rows = rows.Where(x =>
            x.SubscriberCode != null && x.SubscriberCode.Contains(term)
            || x.CustomerName != null && x.CustomerName.Contains(term)
            || x.CustomerGroupName != null && x.CustomerGroupName.Contains(term));
        if (query.CustomerId.HasValue) rows = rows.Where(x => x.CustomerId == query.CustomerId.Value);
        if (query.CustomerGroupId.HasValue) rows = rows.Where(x => x.CustomerGroupId == query.CustomerGroupId.Value);
        if (query.ServiceTypeId.HasValue) rows = rows.Where(x => x.ServiceTypeId == query.ServiceTypeId.Value);
        if (query.CurrencyTypeId.HasValue) rows = rows.Where(x => x.CurrencyTypeId == query.CurrencyTypeId.Value);
        if (query.View == CollectionFollowUpView.Group)
            rows = rows.Where(x => x.CustomerGroupId != null)
                .GroupBy(x => new { x.CustomerGroupId, x.CustomerGroupName, x.CurrencyTypeId, x.CurrencyCode })
                .Select(g => new CollectionTrackingItem
                {
                    ContractId = 0, CustomerId = 0, ServiceTypeId = 0,
                    CustomerGroupId = g.Key.CustomerGroupId,
                    CustomerGroupName = g.Key.CustomerGroupName,
                    Period = period, DueDate = null, SubscriberCode = null,
                    CustomerName = g.Key.CustomerGroupName ?? "Adsız müşteri grubu",
                    ServiceTypeName = "Birden fazla",
                    CurrencyTypeId = g.Key.CurrencyTypeId, CurrencyCode = g.Key.CurrencyCode,
                    AccruedAmount = g.Sum(x => x.AccruedAmount),
                    PaymentAmount = g.Sum(x => x.PaymentAmount),
                    RemainingAmount = g.Sum(x => x.RemainingAmount),
                    HasAccrual = g.Any(x => x.HasAccrual), IsGroup = true,
                    ContractCount = g.Select(x => x.ContractId).Distinct().Count()
                });
        rows = query.BalanceFilter switch
        {
            CollectionTrackingBalanceFilter.Outstanding => rows.Where(x => x.RemainingAmount > 0),
            CollectionTrackingBalanceFilter.NoOutstanding => rows.Where(x => x.RemainingAmount <= 0),
            CollectionTrackingBalanceFilter.PaymentOnly => rows.Where(x => !x.HasAccrual && x.PaymentAmount > 0),
            _ => rows
        };
        return rows;
    }

    private static IOrderedQueryable<CollectionTrackingItem> OrderRows(IQueryable<CollectionTrackingItem> rows,
        CollectionTrackingQuery query)
    {
        var ordered = query.SortBy switch
        {
            CollectionFollowUpSort.CustomerName => query.Desc ? rows.OrderByDescending(x => x.CustomerName) : rows.OrderBy(x => x.CustomerName),
            CollectionFollowUpSort.ServiceTypeName => query.Desc ? rows.OrderByDescending(x => x.ServiceTypeName) : rows.OrderBy(x => x.ServiceTypeName),
            CollectionFollowUpSort.ContractAmount => query.Desc ? rows.OrderByDescending(x => x.AccruedAmount) : rows.OrderBy(x => x.AccruedAmount),
            CollectionFollowUpSort.PaymentAmount => query.Desc ? rows.OrderByDescending(x => x.PaymentAmount) : rows.OrderBy(x => x.PaymentAmount),
            CollectionFollowUpSort.RemainingAmount => query.Desc ? rows.OrderByDescending(x => x.RemainingAmount) : rows.OrderBy(x => x.RemainingAmount),
            _ => query.Desc ? rows.OrderByDescending(x => x.SubscriberCode) : rows.OrderBy(x => x.SubscriberCode)
        };
        return ordered.ThenBy(x => x.CustomerGroupId).ThenBy(x => x.ContractId).ThenBy(x => x.CurrencyTypeId);
    }
}
