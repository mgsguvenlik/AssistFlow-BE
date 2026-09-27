using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.Crm.Collections;

public sealed class CollectionTrackingQuery : IValidatableObject
{
    [Range(1, 1000000, ErrorMessage = "Sayfa numarası 1 ile 1000000 arasında olmalıdır.")]
    public int Page { get; init; } = 1;
    [Range(1, 100, ErrorMessage = "Sayfa boyutu 1 ile 100 arasında olmalıdır.")]
    public int PageSize { get; init; } = 25;
    public DateOnly Period { get; init; }
    /// <summary>Inclusive first month. Omitted means the existing single-month query.</summary>
    public DateOnly? PeriodFrom { get; init; }
    [Range(1, long.MaxValue, ErrorMessage = "Geçerli bir abonelik durumu seçilmelidir.")]
    public long? SubscriptionStatusId { get; init; }
    [Range(1, long.MaxValue, ErrorMessage = "Geçerli bir ödeme yöntemi seçilmelidir.")]
    public long? PaymentMethodId { get; init; }
    public bool ExcludePaymentMethod { get; init; }
    public CollectionFollowUpView View { get; init; } = CollectionFollowUpView.Individual;
    public CollectionTrackingBalanceFilter BalanceFilter { get; init; } = CollectionTrackingBalanceFilter.All;
    [StringLength(200, ErrorMessage = "Arama metni en fazla 200 karakter olabilir.")]
    public string? Search { get; init; }
    [Range(1, long.MaxValue, ErrorMessage = "Geçerli bir müşteri seçilmelidir.")]
    public long? CustomerId { get; init; }
    [Range(1, long.MaxValue, ErrorMessage = "Geçerli bir müşteri grubu seçilmelidir.")]
    public long? CustomerGroupId { get; init; }
    [Range(1, long.MaxValue, ErrorMessage = "Geçerli bir servis tipi seçilmelidir.")]
    public long? ServiceTypeId { get; init; }
    [Range(1, long.MaxValue, ErrorMessage = "Geçerli bir para birimi seçilmelidir.")]
    public long? CurrencyTypeId { get; init; }
    public CollectionFollowUpSort SortBy { get; init; } = CollectionFollowUpSort.SubscriberCode;
    public bool Desc { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (PeriodFrom is { } from && (from == default || from.Day != 1 || from > Period
            || (Period.Year - from.Year) * 12 + Period.Month - from.Month >= 600))
            yield return new ValidationResult("Dönem başlangıcı ayın ilk günü olmalı; aralık ters veya 600 aydan uzun olmamalıdır.", [nameof(PeriodFrom)]);
        if (ExcludePaymentMethod && !PaymentMethodId.HasValue)
            yield return new ValidationResult("Hariç tutulacak ödeme yöntemini seçin.", [nameof(PaymentMethodId)]);
    }
}

public enum CollectionTrackingBalanceFilter
{
    All,
    Outstanding,
    NoOutstanding,
    PaymentOnly
}

public sealed class CollectionTrackingItem
{
    public long ContractId { get; init; }
    public long CustomerId { get; init; }
    public long ServiceTypeId { get; init; }
    public long? CustomerGroupId { get; init; }
    public string? CustomerGroupName { get; init; }
    public DateOnly Period { get; init; }
    public DateOnly? DueDate { get; init; }
    public string? SubscriberCode { get; init; }
    public string? CustomerName { get; init; }
    public string ServiceTypeName { get; init; } = string.Empty;
    public long CurrencyTypeId { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal AccruedAmount { get; init; }
    public decimal PaymentAmount { get; init; }
    public decimal RemainingAmount { get; init; }
    public bool HasAccrual { get; init; }
    public bool IsGroup { get; init; }
    public bool IsGroupCustomer { get; init; }
    public int ContractCount { get; init; }
}
