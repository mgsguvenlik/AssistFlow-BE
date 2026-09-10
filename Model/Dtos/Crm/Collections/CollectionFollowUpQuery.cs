using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.Crm.Collections;

/// <summary>Read-only follow-up query contract. Contains no SQL expressions or tenant scope.</summary>
public sealed class CollectionFollowUpQuery : IValidatableObject
{
    [Range(1, int.MaxValue, ErrorMessage = "Sayfa numarası en az 1 olmalıdır.")]
    public int Page { get; set; } = 1;

    [Range(1, 100, ErrorMessage = "Sayfa başına kayıt sayısı 1 ile 100 arasında olmalıdır.")]
    public int PageSize { get; set; } = 25;

    [StringLength(200, ErrorMessage = "Arama metni en fazla 200 karakter olabilir.")]
    public string? Search { get; set; }

    public CollectionFollowUpView View { get; set; } = CollectionFollowUpView.Individual;
    public CollectionFollowUpSort SortBy { get; set; } = CollectionFollowUpSort.Period;
    public bool Desc { get; set; }
    public DateOnly? PeriodFrom { get; set; }
    public DateOnly? PeriodTo { get; set; }
    public DateOnly? PaymentDateFrom { get; set; }
    public DateOnly? PaymentDateTo { get; set; }
    public DateOnly? AsOfDate { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "Geçerli bir müşteri seçilmelidir.")]
    public long? CustomerId { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "Geçerli bir servis tipi seçilmelidir.")]
    public long? ServiceTypeId { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "Geçerli bir para birimi seçilmelidir.")]
    public long? CurrencyTypeId { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enum.IsDefined(View))
            yield return new ValidationResult("Geçersiz liste görünümü.", [nameof(View)]);
        if (!Enum.IsDefined(SortBy))
            yield return new ValidationResult("Geçersiz sıralama alanı.", [nameof(SortBy)]);
        if (Page > 0 && PageSize > 0 && (long)(Page - 1) * PageSize > int.MaxValue)
            yield return new ValidationResult("İstenen sayfa aralığı desteklenmiyor.", [nameof(Page), nameof(PageSize)]);

        if (PeriodFrom.HasValue != PeriodTo.HasValue)
            yield return new ValidationResult("Dönem başlangıcı ve bitişi birlikte verilmelidir.", [nameof(PeriodFrom), nameof(PeriodTo)]);
        if (PeriodFrom.HasValue && PeriodFrom.Value.Day != 1)
            yield return new ValidationResult("Dönem başlangıcı ayın ilk günü olmalıdır.", [nameof(PeriodFrom)]);
        if (PeriodTo.HasValue && PeriodTo.Value.Day != 1)
            yield return new ValidationResult("Dönem bitişi ayın ilk günüyle belirtilmelidir.", [nameof(PeriodTo)]);
        if (PeriodFrom.HasValue && PeriodTo.HasValue && PeriodFrom.Value > PeriodTo.Value)
            yield return new ValidationResult("Dönem aralığı ters olamaz.", [nameof(PeriodFrom), nameof(PeriodTo)]);

        if (PaymentDateFrom.HasValue != PaymentDateTo.HasValue)
            yield return new ValidationResult("Ödeme tarihi sınırları birlikte verilmelidir.", [nameof(PaymentDateFrom), nameof(PaymentDateTo)]);
        if (PaymentDateFrom.HasValue && PaymentDateTo.HasValue && PaymentDateFrom.Value > PaymentDateTo.Value)
            yield return new ValidationResult("Ödeme tarihi aralığı ters olamaz.", [nameof(PaymentDateFrom), nameof(PaymentDateTo)]);
    }
}

public enum CollectionFollowUpView
{
    Individual,
    Group
}

public enum CollectionFollowUpSort
{
    Period,
    SubscriberCode,
    CustomerName,
    ServiceTypeName,
    ContractAmount,
    PaymentAmount,
    RemainingAmount
}
