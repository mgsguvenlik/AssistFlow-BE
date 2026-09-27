using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.Crm.Collections;

public sealed class CollectionGroupHistoryQuery : IValidatableObject
{
    [Range(1, 1000000, ErrorMessage = "Geçerli sayfa numarası girin.")]
    public int Page { get; set; } = 1;
    [Range(1, 100, ErrorMessage = "Sayfa boyutu 1–100 arasında olmalıdır.")]
    public int PageSize { get; set; } = 25;
    [Range(1, long.MaxValue, ErrorMessage = "Geçerli sözleşme seçin.")]
    public long? ContractId { get; set; }
    [Range(1, long.MaxValue, ErrorMessage = "Geçerli grup seçin.")]
    public long? CustomerGroupId { get; set; }
    [Range(1, long.MaxValue, ErrorMessage = "Geçerli durum seçin.")]
    public long? GroupStatusId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    [StringLength(200, ErrorMessage = "Arama en fazla 200 karakter olabilir.")]
    public string? Search { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (From.HasValue && (From.Value.Day != 1 || From.Value.Year >= 9999)
            || To.HasValue && (To.Value.Day != 1 || To.Value.Year >= 9999) || From > To)
            yield return new("Geçerli başlangıç ve bitiş aylarını seçin.");
    }
}
public sealed record CollectionGroupHistoryItem(long Id, long ContractId, DateOnly Period,
    long CustomerId, string? CustomerName, string? SubscriberCode, string? GroupName,
    string ServiceTypeName, long? GroupStatusId, string? GroupStatusName, string? Description,
    DateTimeOffset CreatedDate, DateTimeOffset? UpdatedDate, bool IsLegacy);

