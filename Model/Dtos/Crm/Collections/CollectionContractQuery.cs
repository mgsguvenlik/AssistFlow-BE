using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.Crm.Collections;

public sealed class CollectionContractQuery : IValidatableObject
{
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 25;
    [Range(1, long.MaxValue)] public long? CustomerId { get; set; }
    [Range(1, long.MaxValue)] public long? ServiceTypeId { get; set; }
    [StringLength(200)] public string? Search { get; set; }
    public CollectionContractSort SortBy { get; set; } = CollectionContractSort.Id;
    public bool Desc { get; set; } = true;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enum.IsDefined(SortBy))
            yield return new("Geçersiz sıralama alanı.", [nameof(SortBy)]);
        if (Page > 0 && PageSize > 0 && (long)(Page - 1) * PageSize > int.MaxValue)
            yield return new("İstenen sayfa aralığı desteklenmiyor.", [nameof(Page), nameof(PageSize)]);
    }
}

public enum CollectionContractSort { Id, SubscriberCode, CustomerName, ServiceTypeName, StartDate }

public sealed class CollectionContractListItem
{
    public long Id { get; init; }
    public long CustomerId { get; init; }
    public string? SubscriberCode { get; init; }
    public string? CustomerName { get; init; }
    public long ServiceTypeId { get; init; }
    public string ServiceTypeName { get; init; } = string.Empty;
    public DateOnly StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
}
