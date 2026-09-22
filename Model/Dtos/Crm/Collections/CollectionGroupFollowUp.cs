using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.Crm.Collections;

public sealed class CollectionGroupFollowUpUpdate : IValidatableObject
{
    public DateOnly Period { get; set; }
    [Range(1, long.MaxValue, ErrorMessage = "Grup durum etiketi seçilmelidir.")]
    public long GroupStatusId { get; set; }
    [StringLength(500, ErrorMessage = "Durum açıklaması en fazla 500 karakter olabilir.")]
    public string? Description { get; set; }
    public byte[]? RowVersion { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Period == default || Period.Day != 1 || Period.Year >= 9999)
            yield return new("Geçerli muhasebe dönemi seçilmelidir.", [nameof(Period)]);
        if (RowVersion is not null && RowVersion.Length != 8)
            yield return new("Kayıt sürümü geçersiz.", [nameof(RowVersion)]);
    }
}

public sealed record CollectionGroupFollowUpItem(long ContractId, DateOnly Period, long? GroupStatusId,
    string? GroupStatusName, string? Description, byte[]? RowVersion);
