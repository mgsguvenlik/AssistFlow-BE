using System.ComponentModel.DataAnnotations;
using Business.Interfaces;
using Business.UnitOfWork;
using Core.Common;
using Microsoft.EntityFrameworkCore;
using Model.Concrete.Collections;
using Model.Dtos.Crm.Collections;

namespace Business.Services.Crm.Collections;

public sealed class CollectionPaymentReportService(IUnitOfWork unitOfWork) : ICollectionPaymentReportService
{
    public async Task<ResponseModel<PagedResult<CollectionPaymentReportItem>>> GetPageAsync(
        CollectionPaymentReportQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(query, new ValidationContext(query), errors, true))
            return ResponseModel<PagedResult<CollectionPaymentReportItem>>.Fail(
                errors.FirstOrDefault()?.ErrorMessage ?? "Rapor filtreleri geçerli değildir.");

        var payments = unitOfWork.Repository.GetQueryable<CollectionPayment>()
            .AsNoTracking()
            .Where(x => x.PaymentDate >= query.StartDate && x.PaymentDate <= query.EndDate
                && !x.Contract.IsDeleted && !x.Contract.Customer.IsDeleted)
            .Select(x => new CollectionPaymentReportItem
            {
                PaymentId = x.Id,
                ContractId = x.ContractId,
                SubscriberCode = x.Contract.Customer.SubscriberCode,
                CustomerName = x.Contract.Customer.SubscriberCompany,
                ServiceTypeName = x.Contract.ServiceType.Name,
                Period = x.Period,
                PaymentDate = x.PaymentDate,
                Amount = x.Amount,
                CurrencyCode = x.CurrencyType.Code,
                Description = x.Description,
                IsFree = x.IsFree
            });

        var term = query.Search?.Trim();
        if (!string.IsNullOrEmpty(term))
            payments = payments.Where(x =>
                x.SubscriberCode != null && x.SubscriberCode.Contains(term)
                || x.CustomerName != null && x.CustomerName.Contains(term));

        var count = await payments.CountAsync(cancellationToken);
        var ordered = query.SortBy switch
        {
            CollectionPaymentReportSort.SubscriberCode => query.Desc
                ? payments.OrderByDescending(x => x.SubscriberCode) : payments.OrderBy(x => x.SubscriberCode),
            CollectionPaymentReportSort.CustomerName => query.Desc
                ? payments.OrderByDescending(x => x.CustomerName) : payments.OrderBy(x => x.CustomerName),
            CollectionPaymentReportSort.ServiceTypeName => query.Desc
                ? payments.OrderByDescending(x => x.ServiceTypeName) : payments.OrderBy(x => x.ServiceTypeName),
            CollectionPaymentReportSort.Period => query.Desc
                ? payments.OrderByDescending(x => x.Period) : payments.OrderBy(x => x.Period),
            CollectionPaymentReportSort.Amount => query.Desc
                ? payments.OrderByDescending(x => x.Amount) : payments.OrderBy(x => x.Amount),
            _ => query.Desc
                ? payments.OrderByDescending(x => x.PaymentDate) : payments.OrderBy(x => x.PaymentDate)
        };
        var items = await ordered.ThenByDescending(x => x.PaymentId)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return ResponseModel<PagedResult<CollectionPaymentReportItem>>.Success(
            new(items, count, query.Page, query.PageSize), "Ödeme raporu getirildi.");
    }
}
