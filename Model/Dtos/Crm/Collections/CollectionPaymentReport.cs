using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.Crm.Collections;

public sealed class CollectionPaymentReportQuery : IValidatableObject
{
    [Range(1, 1000000, ErrorMessage = "Sayfa numarası 1 ile 1000000 arasında olmalıdır.")]
    public int Page { get; init; } = 1;

    [Range(1, 100, ErrorMessage = "Sayfa boyutu 1 ile 100 arasında olmalıdır.")]
    public int PageSize { get; init; } = 25;

    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }

    [StringLength(200, ErrorMessage = "Arama metni en fazla 200 karakter olabilir.")]
    public string? Search { get; init; }

    public CollectionPaymentReportSort SortBy { get; init; } = CollectionPaymentReportSort.PaymentDate;
    public bool Desc { get; init; } = true;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartDate == default || EndDate == default)
            yield return new ValidationResult("Başlangıç ve bitiş tarihi gereklidir.");
        else if (EndDate < StartDate)
            yield return new ValidationResult("Bitiş tarihi başlangıç tarihinden önce olamaz.");
        if (!Enum.IsDefined(SortBy))
            yield return new ValidationResult("Geçerli bir sıralama alanı seçilmelidir.");
    }
}

public enum CollectionPaymentReportSort
{
    PaymentDate,
    SubscriberCode,
    CustomerName,
    ServiceTypeName,
    Period,
    Amount
}

public sealed class CollectionPaymentReportItem
{
    public long PaymentId { get; init; }
    public long ContractId { get; init; }
    public string? SubscriberCode { get; init; }
    public string? CustomerName { get; init; }
    public string ServiceTypeName { get; init; } = string.Empty;
    public DateOnly Period { get; init; }
    public DateOnly PaymentDate { get; init; }
    public decimal Amount { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsFree { get; init; }
}
