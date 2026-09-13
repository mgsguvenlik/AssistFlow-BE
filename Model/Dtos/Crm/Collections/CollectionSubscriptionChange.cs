using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.Crm.Collections;

public sealed class CollectionSubscriptionChange : IValidatableObject
{
    public bool Freeze { get; set; }
    [Required(ErrorMessage = "Kayıt sürümü gereklidir.")]
    public byte[] RowVersion { get; set; } = [];
    [Required(ErrorMessage = "İşlem açıklaması gereklidir.")]
    [StringLength(70, ErrorMessage = "İşlem açıklaması en fazla 70 karakter olabilir.")]
    public string Reason { get; set; } = string.Empty;
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (RowVersion is not { Length: 8 }) yield return new("Kayıt sürümü geçersiz.", [nameof(RowVersion)]);
        if (string.IsNullOrWhiteSpace(Reason)) yield return new("İşlem açıklaması gereklidir.", [nameof(Reason)]);
    }
}

public sealed record CollectionSubscriptionChanged(long ContractId, DateOnly EffectiveFrom);
