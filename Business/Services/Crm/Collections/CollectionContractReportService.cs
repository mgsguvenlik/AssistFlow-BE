using System.ComponentModel.DataAnnotations;
using Business.Interfaces;
using Business.UnitOfWork;
using Core.Common;
using Microsoft.EntityFrameworkCore;
using Model.Concrete.Collections;
using Model.Dtos.Crm.Collections;

namespace Business.Services.Crm.Collections;

public sealed class CollectionContractReportService(IUnitOfWork unitOfWork) : ICollectionContractReportService
{
    public async Task<ResponseModel<PagedResult<CollectionContractReportItem>>> GetPageAsync(
        CollectionContractReportQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(query, new ValidationContext(query), errors, true))
            return ResponseModel<PagedResult<CollectionContractReportItem>>.Fail(
                errors.FirstOrDefault()?.ErrorMessage ?? "Rapor filtreleri geçerli değildir.");

        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(
            DateTimeOffset.UtcNow, "Turkey Standard Time").DateTime);
        var repository = unitOfWork.Repository;
        var rates = repository.GetQueryable<CollectionContractRatePeriod>().AsNoTracking();
        var contracts = repository.GetQueryable<CollectionContract>()
            .AsNoTracking()
            .Where(x => !x.IsDeleted && !x.Customer.IsDeleted)
            .Select(x => new
            {
                Contract = x,
                Rate = rates.Where(r => r.ContractId == x.Id && !r.IsDeleted
                        && r.EffectiveFrom <= today
                        && (!r.EffectiveToExclusive.HasValue || r.EffectiveToExclusive > today))
                    .OrderByDescending(r => r.EffectiveFrom).ThenByDescending(r => r.Id)
                    .Select(r => new
                    {
                        r.Amount,
                        CurrencyCode = r.CurrencyType == null ? null : r.CurrencyType.Code,
                        PaymentFrequencyName = r.PaymentFrequency.Name,
                        r.BillingBehavior
                    }).FirstOrDefault()
            })
            .Select(x => new CollectionContractReportItem
            {
                ContractId = x.Contract.Id,
                SubscriberCode = x.Contract.Customer.SubscriberCode,
                CustomerName = x.Contract.Customer.SubscriberCompany,
                ServiceTypeName = x.Contract.ServiceType.Name,
                ContractStatusName = x.Contract.ContractStatus == null ? null : x.Contract.ContractStatus.Name,
                SubscriptionStatusName = x.Contract.SubscriptionStatus == null ? null : x.Contract.SubscriptionStatus.Name,
                StartDate = x.Contract.StartDate,
                EndDate = x.Contract.EndDate,
                GtsNo = x.Contract.GtsNo,
                IvrNo = x.Contract.IvrNo,
                Amount = x.Rate == null ? null : x.Rate.Amount,
                CurrencyCode = x.Rate == null ? null : x.Rate.CurrencyCode,
                PaymentFrequencyName = x.Rate == null ? null : x.Rate.PaymentFrequencyName,
                IsFree = x.Rate != null && x.Rate.BillingBehavior == CollectionBillingBehavior.Free,
                RowVersion = x.Contract.RowVersion
            });

        var term = query.Search?.Trim();
        if (!string.IsNullOrEmpty(term))
            contracts = contracts.Where(x =>
                x.SubscriberCode != null && x.SubscriberCode.Contains(term)
                || x.CustomerName != null && x.CustomerName.Contains(term)
                || x.GtsNo != null && x.GtsNo.Contains(term)
                || x.IvrNo != null && x.IvrNo.Contains(term));

        var count = await contracts.CountAsync(cancellationToken);
        var ordered = query.SortBy switch
        {
            CollectionContractReportSort.SubscriberCode => query.Desc
                ? contracts.OrderByDescending(x => x.SubscriberCode) : contracts.OrderBy(x => x.SubscriberCode),
            CollectionContractReportSort.CustomerName => query.Desc
                ? contracts.OrderByDescending(x => x.CustomerName) : contracts.OrderBy(x => x.CustomerName),
            CollectionContractReportSort.ServiceTypeName => query.Desc
                ? contracts.OrderByDescending(x => x.ServiceTypeName) : contracts.OrderBy(x => x.ServiceTypeName),
            CollectionContractReportSort.Amount => query.Desc
                ? contracts.OrderByDescending(x => x.Amount) : contracts.OrderBy(x => x.Amount),
            _ => query.Desc
                ? contracts.OrderByDescending(x => x.StartDate) : contracts.OrderBy(x => x.StartDate)
        };
        var items = await ordered.ThenByDescending(x => x.ContractId)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return ResponseModel<PagedResult<CollectionContractReportItem>>.Success(
            new(items, count, query.Page, query.PageSize), "Sözleşme raporu getirildi.");
    }
}
