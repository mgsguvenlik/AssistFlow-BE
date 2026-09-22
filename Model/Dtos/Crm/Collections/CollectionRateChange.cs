using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.Crm.Collections;

public sealed class CollectionRateChange : IValidatableObject
{
    public decimal Amount { get; set; }
    public bool IsFree { get; set; }
    [Required(ErrorMessage = "Kayıt sürümü gereklidir.")]
    public byte[] RowVersion { get; set; } = [];
    [Required(ErrorMessage = "İşlem açıklaması gereklidir.")]
    [StringLength(70, ErrorMessage = "İşlem açıklaması en fazla 70 karakter olabilir.")]
    public string Reason { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (RowVersion is not { Length: 8 }) yield return new("Kayıt sürümü geçersiz.", [nameof(RowVersion)]);
        if (string.IsNullOrWhiteSpace(Reason)) yield return new("İşlem açıklaması gereklidir.", [nameof(Reason)]);
        if (Amount < 0 || Amount > 9999999999999999.99m || decimal.Round(Amount, 2) != Amount)
            yield return new("Dönem tutarı geçersiz; en fazla iki ondalık basamak girin.", [nameof(Amount)]);
        if (!IsFree && Amount <= 0) yield return new("Ücretli hizmet için dönem tutarı sıfırdan büyük olmalıdır.", [nameof(Amount)]);
    }
}

public sealed record CollectionRateChanged(long ContractId, DateOnly EffectiveFrom, DateOnly NextDueDate);
