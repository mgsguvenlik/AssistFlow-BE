using System.ComponentModel.DataAnnotations;
using Business.Interfaces;
using Data.Concrete;
using Core.Common;
using Core.Enums;
using Data.Concrete.EfCore.Collections;
using Data.Concrete.EfCore.Context;
using Model.Concrete;
using Microsoft.EntityFrameworkCore;
using Model.Concrete.Collections;
using Model.Dtos.Crm.Collections;

namespace Business.Services.Crm.Collections;

public sealed class CollectionTrackingService(AppDataContext db) : ICollectionTrackingService
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
        // The shared repository factory creates its own context. Bind this composed query to one
        // existing repository instance on the injected context, including its SQL calendar source.
        var repository = new Repository(db);
        var firstPeriod = query.PeriodFrom ?? query.Period;
        var until = query.Period.AddMonths(1);
        var monthCount = (query.Period.Year - firstPeriod.Year) * 12 + query.Period.Month - firstPeriod.Month + 1;
        // Only the small calendar is built locally. Financial rows, grouping, filtering and paging stay in SQL.
        var periods = CollectionTrackingCalendarQuery.Months(db, firstPeriod, monthCount);
        var kind = query.View == CollectionFollowUpView.Group || query.CustomerGroupId.HasValue
            ? CollectionCustomerClass.Group : CollectionCustomerClass.Individual;
        var contracts = CollectionCustomerScopeQuery.Contracts(repository.GetQueryable<CollectionContract>(),
            repository.GetQueryable<Customer>(), kind).AsNoTracking();
        if (query.CustomerId.HasValue) contracts = contracts.Where(x => x.CustomerId == query.CustomerId);
        if (query.CustomerGroupId.HasValue) contracts = contracts.Where(x => x.Customer.CustomerGroupId == query.CustomerGroupId);
        if (query.ServiceTypeId.HasValue) contracts = contracts.Where(x => x.ServiceTypeId == query.ServiceTypeId);
        if (query.SubscriptionStatusId.HasValue) contracts = contracts.Where(x => x.SubscriptionStatusId == query.SubscriptionStatusId);
        if (query.PaymentMethodId.HasValue)
            contracts = query.ExcludePaymentMethod
                ? contracts.Where(x => x.PaymentMethodId == null || x.PaymentMethodId != query.PaymentMethodId)
                : contracts.Where(x => x.PaymentMethodId == query.PaymentMethodId);
        var term = query.Search?.Trim();
        if (!string.IsNullOrEmpty(term)) contracts = contracts.Where(x =>
            x.Customer.SubscriberCode != null && x.Customer.SubscriberCode.Contains(term)
            || x.Customer.SubscriberCompany != null && x.Customer.SubscriberCompany.Contains(term)
            || x.Customer.CustomerGroup != null && (x.Customer.CustomerGroup.GroupName.Contains(term)
                || x.Customer.CustomerGroup.Code.Contains(term))
            || x.GtsNo != null && x.GtsNo.Contains(term) || x.IvrNo != null && x.IvrNo.Contains(term)
            || x.Customer.Phone1 != null && x.Customer.Phone1.Contains(term)
            || x.Customer.Phone2 != null && x.Customer.Phone2.Contains(term)
            || x.Customer.Email1 != null && x.Customer.Email1.Contains(term)
            || x.Customer.Email2 != null && x.Customer.Email2.Contains(term)
            || x.Customer.City != null && x.Customer.City.Contains(term)
            || x.Customer.Note != null && x.Customer.Note.Contains(term));
        var contractIds = contracts.Select(x => x.Id);
        var payments = repository.GetQueryable<CollectionPayment>().AsNoTracking()
            .Where(x => x.Period >= firstPeriod && x.Period < until && contractIds.Contains(x.ContractId)
                && (!query.CurrencyTypeId.HasValue || x.CurrencyTypeId == query.CurrencyTypeId))
            .Select(x => new { x.ContractId, x.Period, x.CurrencyTypeId, x.Amount });
        var rates = repository.GetQueryable<CollectionContractRatePeriod>().AsNoTracking()
            .Where(x => !x.IsDeleted && x.BillingBehavior == CollectionBillingBehavior.Billable
                && x.Amount != null && x.CurrencyTypeId != null && contractIds.Contains(x.ContractId)
                && x.EffectiveFrom < until && (x.EffectiveToExclusive == null || x.EffectiveToExclusive > firstPeriod)
                && (x.Contract.EndDate == null || x.Contract.EndDate >= firstPeriod)
                && (!query.CurrencyTypeId.HasValue || x.CurrencyTypeId == query.CurrencyTypeId));
        var candidates = rates.SelectMany(rate => periods, (rate, period) => new
        {
            Rate = rate, Period = period,
            MonthDifference = EF.Functions.DateDiffMonth(rate.BillingAnchor, period),
            AnchorDay = rate.OriginalAnchorDay ?? (byte)rate.BillingAnchor.Day,
            MonthDays = period.AddMonths(1).AddDays(-1).Day
        }).Where(x => x.Period.AddMonths(1) > x.Rate.EffectiveFrom
                && (x.Rate.EffectiveToExclusive == null || x.Period < x.Rate.EffectiveToExclusive)
                && (x.Rate.Contract.EndDate == null || x.Period <= x.Rate.Contract.EndDate)
                && x.MonthDifference >= 0 && x.MonthDifference % x.Rate.PaymentFrequency.IntervalMonths == 0)
            .Select(x => new
            {
                x.Rate, x.Period,
                DueDate = x.Period.AddDays((x.AnchorDay > x.MonthDays ? x.MonthDays : x.AnchorDay) - 1)
            }).Where(x => x.Rate.EffectiveFrom <= x.DueDate
                && (x.Rate.EffectiveToExclusive == null || x.Rate.EffectiveToExclusive > x.DueDate)
                && (x.Rate.Contract.EndDate == null || x.Rate.Contract.EndDate >= x.DueDate));
        // UNION ALL contributions once, then aggregate once. Rejoining two aggregates through their
        // unioned keys repeats the full calendar scan and payment grouping for long historical ranges.
        var contributions = candidates.Select(x => new
        {
            x.Rate.ContractId, x.Period, CurrencyTypeId = x.Rate.CurrencyTypeId!.Value,
            DueDate = (DateOnly?)x.DueDate, AccruedAmount = x.Rate.Amount!.Value, PaymentAmount = 0m
        }).Concat(payments.Select(x => new
        {
            x.ContractId, x.Period, x.CurrencyTypeId,
            DueDate = (DateOnly?)null, AccruedAmount = 0m, PaymentAmount = x.Amount
        }));
        var balances = contributions.GroupBy(x => new { x.ContractId, x.Period, x.CurrencyTypeId })
            .Select(g => new
            {
                g.Key.ContractId, g.Key.Period, g.Key.CurrencyTypeId,
                DueDate = g.Min(x => x.DueDate),
                AccruedAmount = g.Sum(x => x.AccruedAmount), PaymentAmount = g.Sum(x => x.PaymentAmount)
            });
        var currencies = repository.GetQueryable<Model.Concrete.CurrencyType>().AsNoTracking();
        IQueryable<CollectionTrackingItem> rows =
            from balance in balances
            join contract in contracts on balance.ContractId equals contract.Id
            join currency in currencies on balance.CurrencyTypeId equals currency.Id
            select new CollectionTrackingItem
            {
                ContractId = contract.Id, CustomerId = contract.CustomerId, ServiceTypeId = contract.ServiceTypeId,
                CustomerGroupId = contract.Customer.CustomerGroupId,
                CustomerGroupName = contract.Customer.CustomerGroup == null ? null : contract.Customer.CustomerGroup.GroupName,
                Period = balance.Period, DueDate = balance.DueDate,
                SubscriberCode = contract.Customer.SubscriberCode, CustomerName = contract.Customer.SubscriberCompany,
                ServiceTypeName = contract.ServiceType.Name, CurrencyTypeId = balance.CurrencyTypeId, CurrencyCode = currency.Code,
                AccruedAmount = balance.AccruedAmount, PaymentAmount = balance.PaymentAmount,
                RemainingAmount = balance.AccruedAmount - balance.PaymentAmount,
                HasAccrual = balance.DueDate != null, IsGroup = false,
                IsGroupCustomer = kind == CollectionCustomerClass.Group, ContractCount = 1
            };
        if (query.View == CollectionFollowUpView.Group)
            rows = rows.Where(x => x.CustomerGroupId != null)
                .GroupBy(x => new { x.CustomerGroupId, x.CustomerGroupName, x.Period, x.CurrencyTypeId, x.CurrencyCode })
                .Select(g => new CollectionTrackingItem
                {
                    ContractId = 0, CustomerId = 0, ServiceTypeId = 0,
                    CustomerGroupId = g.Key.CustomerGroupId, CustomerGroupName = g.Key.CustomerGroupName,
                    Period = g.Key.Period, DueDate = null, SubscriberCode = null,
                    CustomerName = g.Key.CustomerGroupName ?? "Adsız müşteri grubu", ServiceTypeName = "Birden fazla",
                    CurrencyTypeId = g.Key.CurrencyTypeId, CurrencyCode = g.Key.CurrencyCode,
                    AccruedAmount = g.Sum(x => x.AccruedAmount), PaymentAmount = g.Sum(x => x.PaymentAmount),
                    RemainingAmount = g.Sum(x => x.RemainingAmount), HasAccrual = g.Any(x => x.HasAccrual),
                    IsGroup = true, IsGroupCustomer = true, ContractCount = g.Select(x => x.ContractId).Distinct().Count()
                });
        return query.BalanceFilter switch
        {
            CollectionTrackingBalanceFilter.Outstanding => rows.Where(x => x.RemainingAmount > 0),
            CollectionTrackingBalanceFilter.NoOutstanding => rows.Where(x => x.RemainingAmount <= 0),
            CollectionTrackingBalanceFilter.PaymentOnly => rows.Where(x => !x.HasAccrual && x.PaymentAmount > 0),
            _ => rows
        };
    }

    private static IOrderedQueryable<CollectionTrackingItem> OrderRows(IQueryable<CollectionTrackingItem> rows,
        CollectionTrackingQuery query)
    {
        var ordered = query.SortBy switch
        {
            CollectionFollowUpSort.Period => query.Desc ? rows.OrderByDescending(x => x.Period) : rows.OrderBy(x => x.Period),
            CollectionFollowUpSort.CustomerName => query.Desc ? rows.OrderByDescending(x => x.CustomerName) : rows.OrderBy(x => x.CustomerName),
            CollectionFollowUpSort.ServiceTypeName => query.Desc ? rows.OrderByDescending(x => x.ServiceTypeName) : rows.OrderBy(x => x.ServiceTypeName),
            CollectionFollowUpSort.ContractAmount => query.Desc ? rows.OrderByDescending(x => x.AccruedAmount) : rows.OrderBy(x => x.AccruedAmount),
            CollectionFollowUpSort.PaymentAmount => query.Desc ? rows.OrderByDescending(x => x.PaymentAmount) : rows.OrderBy(x => x.PaymentAmount),
            CollectionFollowUpSort.RemainingAmount => query.Desc ? rows.OrderByDescending(x => x.RemainingAmount) : rows.OrderBy(x => x.RemainingAmount),
            _ => query.Desc ? rows.OrderByDescending(x => x.SubscriberCode) : rows.OrderBy(x => x.SubscriberCode)
        };
        return ordered.ThenBy(x => x.Period).ThenBy(x => x.CustomerGroupId).ThenBy(x => x.ContractId).ThenBy(x => x.CurrencyTypeId);
    }
}
