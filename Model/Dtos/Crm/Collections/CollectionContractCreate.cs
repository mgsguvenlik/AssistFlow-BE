using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.Crm.Collections;

public sealed class CollectionContractCreate : IValidatableObject
{
    public Guid RequestId { get; set; }
    [Range(1, long.MaxValue, ErrorMessage = "Müşteri seçilmelidir.")] public long CustomerId { get; set; }
    [Range(1, long.MaxValue, ErrorMessage = "Servis tipi seçilmelidir.")] public long ServiceTypeId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    [Range(1, long.MaxValue, ErrorMessage = "Sözleşme durumu geçersiz.")] public long? ContractStatusId { get; set; }
    [Range(1, long.MaxValue, ErrorMessage = "Abonelik durumu seçilmelidir.")] public long SubscriptionStatusId { get; set; }
    [Range(1, long.MaxValue, ErrorMessage = "Ödeme dönemi seçilmelidir.")] public long PaymentFrequencyId { get; set; }
    [Range(1, long.MaxValue, ErrorMessage = "Para birimi seçilmelidir.")] public long CurrencyTypeId { get; set; }
    public decimal Amount { get; set; }
    public bool IsFree { get; set; }
    [StringLength(50, ErrorMessage = "GTS numarası en fazla 50 karakter olabilir.")] public string? GtsNo { get; set; }
    [StringLength(50, ErrorMessage = "IVR numarası en fazla 50 karakter olabilir.")] public string? IvrNo { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (RequestId == Guid.Empty) yield return new("İşlem anahtarı gereklidir.", [nameof(RequestId)]);
        if (StartDate == DateOnly.MinValue || StartDate.Year == 9999)
            yield return new("Başlangıç tarihi geçersiz.", [nameof(StartDate)]);
        if (EndDate < StartDate || EndDate == DateOnly.MaxValue)
            yield return new("Bitiş tarihi başlangıçtan önce veya desteklenen aralık dışında olamaz.", [nameof(EndDate)]);
        if (Amount < 0 || Amount > 9999999999999999.99m || decimal.Round(Amount, 2) != Amount)
            yield return new("Dönem tutarı negatif olamaz ve en fazla iki ondalık basamak içerebilir.", [nameof(Amount)]);
    }
}

public sealed record CollectionContractCreated(long ContractId, bool Replayed);
