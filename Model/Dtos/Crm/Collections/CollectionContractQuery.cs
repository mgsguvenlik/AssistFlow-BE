using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.Crm.Collections;

public sealed class CollectionContractQuery : IValidatableObject
{
    [Range(1, int.MaxValue, ErrorMessage = "Sayfa numarası en az 1 olmalıdır.")] public int Page { get; set; } = 1;
    [Range(1, 100, ErrorMessage = "Sayfa başına kayıt sayısı 1 ile 100 arasında olmalıdır.")] public int PageSize { get; set; } = 25;
    [Range(1, long.MaxValue, ErrorMessage = "Geçerli bir müşteri seçilmelidir.")] public long? CustomerId { get; set; }
    [Range(1, long.MaxValue, ErrorMessage = "Geçerli bir servis tipi seçilmelidir.")] public long? ServiceTypeId { get; set; }
    [Range(1, long.MaxValue, ErrorMessage = "Geçerli bir sözleşme durumu seçilmelidir.")] public long? ContractStatusId { get; set; }
    [StringLength(200, ErrorMessage = "Arama metni en fazla 200 karakter olabilir.")] public string? Search { get; set; }
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

public class CollectionContractListItem
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

public sealed class CollectionContractDetail : CollectionContractListItem
{
    public string? GtsNo { get; init; }
    public string? IvrNo { get; init; }
    public string? ContractStatusName { get; init; }
    public string? SubscriptionStatusName { get; init; }
    public string? SubscriptionStatusCode { get; init; }
    public byte[] RowVersion { get; init; } = [];
}

public sealed class CollectionRateHistoryQuery
{
    [Range(1, 1000000, ErrorMessage = "Sayfa numarası 1 ile 1000000 arasında olmalıdır.")]
    public int Page { get; set; } = 1;
    [Range(1, 100, ErrorMessage = "Sayfa boyutu 1 ile 100 arasında olmalıdır.")]
    public int PageSize { get; set; } = 25;
}

public sealed class CollectionRateHistoryItem
{
    public long Id { get; init; }
    public DateOnly EffectiveFrom { get; init; }
    public DateOnly? EffectiveToExclusive { get; init; }
    public DateOnly BillingAnchor { get; init; }
    public string PaymentFrequencyName { get; init; } = string.Empty;
    public decimal? Amount { get; init; }
    public string? CurrencyCode { get; init; }
    public global::Model.Concrete.Collections.CollectionBillingBehavior BillingBehavior { get; init; }
    public string? ChangeReason { get; init; }
}
