using System.ComponentModel.DataAnnotations;
using Model.Dtos.Crm.Collections;

namespace Business.Services.Crm.Collections.Queries;

/// <summary>
/// Composes ordering and paging without executing or materializing the source.
/// Caller must filter first and supply one row per unique contract/period/rate tuple.
/// This projection is not an EF entity or an API response contract.
/// </summary>
public static class CollectionFollowUpPaging
{
    public static IQueryable<CollectionFollowUpReadRow> Apply(
        IQueryable<CollectionFollowUpReadRow> source, CollectionFollowUpQuery query)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(query);
        Validator.ValidateObject(query, new ValidationContext(query), validateAllProperties: true);

        var ordered = query.SortBy switch
        {
            CollectionFollowUpSort.Period => query.Desc ? source.OrderByDescending(x => x.Period) : source.OrderBy(x => x.Period),
            CollectionFollowUpSort.SubscriberCode => query.Desc ? source.OrderByDescending(x => x.SubscriberCode) : source.OrderBy(x => x.SubscriberCode),
            CollectionFollowUpSort.CustomerName => query.Desc ? source.OrderByDescending(x => x.CustomerName) : source.OrderBy(x => x.CustomerName),
            CollectionFollowUpSort.ServiceTypeName => query.Desc ? source.OrderByDescending(x => x.ServiceTypeName) : source.OrderBy(x => x.ServiceTypeName),
            CollectionFollowUpSort.ContractAmount => query.Desc ? source.OrderByDescending(x => x.ContractAmount) : source.OrderBy(x => x.ContractAmount),
            CollectionFollowUpSort.PaymentAmount => query.Desc ? source.OrderByDescending(x => x.PaymentAmount) : source.OrderBy(x => x.PaymentAmount),
            CollectionFollowUpSort.RemainingAmount => query.Desc ? source.OrderByDescending(x => x.RemainingAmount) : source.OrderBy(x => x.RemainingAmount),
            _ => throw new ArgumentOutOfRangeException(nameof(query.SortBy))
        };

        // Fixed tie breakers prevent equal display values from moving between pages in an unchanged dataset.
        return ordered.ThenBy(x => x.ContractId).ThenBy(x => x.Period).ThenBy(x => x.RatePeriodId)
            .Skip(checked((query.Page - 1) * query.PageSize)).Take(query.PageSize);
    }
}

public sealed class CollectionFollowUpReadRow
{
    public long ContractId { get; init; }
    public long RatePeriodId { get; init; }
    public DateOnly Period { get; init; }
    public string SubscriberCode { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public string ServiceTypeName { get; init; } = string.Empty;
    public decimal? ContractAmount { get; init; }
    public decimal? PaymentAmount { get; init; }
    public decimal? RemainingAmount { get; init; }
}
