using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.Crm.Collections;

public sealed class CollectionContractIdentityUpdate : IValidatableObject
{
    [Range(1, long.MaxValue, ErrorMessage = "Servis tipi seçilmelidir.")]
    public long ServiceTypeId { get; set; }
    [StringLength(50, ErrorMessage = "GTS numarası en fazla 50 karakter olabilir.")]
    public string? GtsNo { get; set; }
    [StringLength(50, ErrorMessage = "IVR numarası en fazla 50 karakter olabilir.")]
    public string? IvrNo { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (RowVersion is not { Length: 8 }) yield return new("Kayıt sürümü geçersiz. Detayı yenileyin.", [nameof(RowVersion)]);
    }
}

public sealed record CollectionContractIdentityUpdated(long ContractId);
