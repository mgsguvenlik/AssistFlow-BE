using System.ComponentModel.DataAnnotations;
using Business.Interfaces;
using Business.UnitOfWork;
using Core.Common;
using Microsoft.EntityFrameworkCore;
using Model.Concrete.Collections;
using Model.Dtos.Crm.Collections;

namespace Business.Services.Crm.Collections;

/// <summary>Active choices only. Historical details must not use this list to erase inactive references.</summary>
public sealed class CollectionDefinitionReadService(IUnitOfWork unitOfWork) : ICollectionDefinitionReadService
{
    public async Task<ResponseModel<PagedResult<CollectionDefinitionItem>>> GetPageAsync(CollectionDefinitionQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(query, new ValidationContext(query), errors, true))
            return ResponseModel<PagedResult<CollectionDefinitionItem>>.Fail(string.Join(" ", errors.Select(x => x.ErrorMessage)));
        var repository = unitOfWork.Repository;
        IQueryable<CollectionDefinitionItem> source = query.Kind switch
        {
            CollectionDefinitionKind.PaymentFrequency => repository.GetQueryable<CollectionPaymentFrequency>().AsNoTracking()
                .Where(x => x.IsActive).Select(x => new CollectionDefinitionItem { Id = x.Id, Code = x.Code, Name = x.Name, IntervalMonths = x.IntervalMonths }),
            CollectionDefinitionKind.ContractStatus => repository.GetQueryable<CollectionContractStatus>().AsNoTracking()
                .Where(x => x.IsActive).Select(x => new CollectionDefinitionItem { Id = x.Id, Code = x.Code, Name = x.Name }),
            CollectionDefinitionKind.SubscriptionStatus => repository.GetQueryable<CollectionSubscriptionStatus>().AsNoTracking()
                .Where(x => x.IsActive).Select(x => new CollectionDefinitionItem { Id = x.Id, Code = x.Code, Name = x.Name }),
            CollectionDefinitionKind.GroupStatus => repository.GetQueryable<CollectionGroupStatus>().AsNoTracking()
                .Where(x => x.IsActive).Select(x => new CollectionDefinitionItem { Id = x.Id, Code = x.Code, Name = x.Name }),
            _ => repository.GetQueryable<CollectionPaymentMethod>().AsNoTracking()
                .Where(x => x.IsActive).Select(x => new CollectionDefinitionItem { Id = x.Id, Code = x.Code, Name = x.Name })
        };
        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
            source = source.Where(x => x.Name.Contains(search) || x.Code.Contains(search));
        var count = await source.CountAsync(cancellationToken);
        var sorted = query.Kind == CollectionDefinitionKind.PaymentFrequency
            ? source.OrderBy(x => x.IntervalMonths).ThenBy(x => x.Name).ThenBy(x => x.Id)
            : source.OrderBy(x => x.Name).ThenBy(x => x.Id);
        var rows = await sorted
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(cancellationToken);
        return ResponseModel<PagedResult<CollectionDefinitionItem>>.Success(new(rows, count, query.Page, query.PageSize),
            "Tahsilat tanımları başarıyla getirildi.");
    }
}
