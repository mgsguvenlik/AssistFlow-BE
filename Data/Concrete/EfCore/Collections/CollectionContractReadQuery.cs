using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Model.Concrete.Collections;
using Model.Dtos.Crm.Collections;

namespace Data.Concrete.EfCore.Collections;

/// <summary>Read-only SQL composition. Does not open connections or register the draft EF model.</summary>
public static class CollectionContractReadQuery
{
    public static IQueryable<CollectionContract> Filter(IQueryable<CollectionContract> source, CollectionContractQuery query)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(query);
        Validator.ValidateObject(query, new ValidationContext(query), true);
        var filtered = source.AsNoTracking().Where(x => !x.IsDeleted);
        if (query.CustomerId.HasValue) filtered = filtered.Where(x => x.CustomerId == query.CustomerId.Value);
        if (query.ServiceTypeId.HasValue) filtered = filtered.Where(x => x.ServiceTypeId == query.ServiceTypeId.Value);
        var term = query.Search?.Trim();
        if (!string.IsNullOrEmpty(term))
            filtered = filtered.Where(x => (x.Customer.SubscriberCode != null && x.Customer.SubscriberCode.Contains(term))
                || (x.Customer.SubscriberCompany != null && x.Customer.SubscriberCompany.Contains(term)));
        // Do not discard contracts merely because a referenced shared definition was soft-deleted.
        return filtered;
    }

    public static IQueryable<CollectionContractListItem> Page(IQueryable<CollectionContract> source, CollectionContractQuery query)
    {
        var filtered = Filter(source, query);
        var ordered = query.SortBy switch
        {
            CollectionContractSort.Id => query.Desc ? filtered.OrderByDescending(x => x.Id) : filtered.OrderBy(x => x.Id),
            CollectionContractSort.SubscriberCode => query.Desc ? filtered.OrderByDescending(x => x.Customer.SubscriberCode) : filtered.OrderBy(x => x.Customer.SubscriberCode),
            CollectionContractSort.CustomerName => query.Desc ? filtered.OrderByDescending(x => x.Customer.SubscriberCompany) : filtered.OrderBy(x => x.Customer.SubscriberCompany),
            CollectionContractSort.ServiceTypeName => query.Desc ? filtered.OrderByDescending(x => x.ServiceType.Name) : filtered.OrderBy(x => x.ServiceType.Name),
            CollectionContractSort.StartDate => query.Desc ? filtered.OrderByDescending(x => x.StartDate) : filtered.OrderBy(x => x.StartDate),
            _ => throw new ArgumentOutOfRangeException(nameof(query.SortBy))
        };
        return Project(ordered.ThenBy(x => x.Id).Skip(checked((query.Page - 1) * query.PageSize)).Take(query.PageSize));
    }

    public static IQueryable<CollectionContractListItem> Detail(IQueryable<CollectionContract> source, long id)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
        return Project(source.AsNoTracking().Where(x => !x.IsDeleted && x.Id == id));
    }

    private static IQueryable<CollectionContractListItem> Project(IQueryable<CollectionContract> source) =>
        source.Select(x => new CollectionContractListItem
        {
            Id = x.Id, CustomerId = x.CustomerId, SubscriberCode = x.Customer.SubscriberCode,
            CustomerName = x.Customer.SubscriberCompany, ServiceTypeId = x.ServiceTypeId,
            ServiceTypeName = x.ServiceType.Name, StartDate = x.StartDate, EndDate = x.EndDate
        });
}
