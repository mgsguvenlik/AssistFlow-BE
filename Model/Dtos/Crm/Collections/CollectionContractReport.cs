using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.Crm.Collections;

public sealed class CollectionContractReportQuery : IValidatableObject
{
    [Range(1, 1000000, ErrorMessage = "Sayfa numarası 1 ile 1000000 arasında olmalıdır.")]
    public int Page { get; init; } = 1;

    [Range(1, 100, ErrorMessage = "Sayfa boyutu 1 ile 100 arasında olmalıdır.")]
    public int PageSize { get; init; } = 25;

    [StringLength(200, ErrorMessage = "Arama metni en fazla 200 karakter olabilir.")]
    public string? Search { get; init; }

    public CollectionContractReportSort SortBy { get; init; } = CollectionContractReportSort.StartDate;
    public bool Desc { get; init; } = true;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enum.IsDefined(SortBy))
            yield return new ValidationResult("Geçerli bir sıralama alanı seçilmelidir.");
    }
}

public enum CollectionContractReportSort
{
    StartDate,
    SubscriberCode,
    CustomerName,
    ServiceTypeName,
    Amount
}

public sealed class CollectionContractReportItem
{
    public long ContractId { get; init; }
    public string? SubscriberCode { get; init; }
    public string? CustomerName { get; init; }
    public string ServiceTypeName { get; init; } = string.Empty;
    public string? ContractStatusName { get; init; }
    public string? SubscriptionStatusName { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public string? GtsNo { get; init; }
    public string? IvrNo { get; init; }
    public decimal? Amount { get; init; }
    public string? CurrencyCode { get; init; }
    public string? PaymentFrequencyName { get; init; }
    public bool IsFree { get; init; }
    public byte[] RowVersion { get; init; } = [];
}
