using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.Crm.Collections;

public sealed record CollectionCustomerCard(long Id, string? SubscriberCode, string? Name,
    string? Address, string? City, string? District, string? ContactName1, string? Phone1, string? Email1,
    string? ContactName2, string? Phone2, string? Email2, string? Note, string? CustomerType,
    long? GroupId, string? GroupName, bool IsGroupParent, bool IsGroup);

public sealed record CollectionCustomerNoteItem(long Id, string Text, DateTimeOffset CreatedDate,
    DateTimeOffset? UpdatedDate, long CreatedUser, long? UpdatedUser, byte[] RowVersion,
    string? LegacyCreatedBy, string? LegacyModifiedBy, DateTime? LegacyCreatedOn, DateTime? LegacyModifiedOn);

public sealed class CollectionCustomerNoteCreate
{
    [Required(ErrorMessage = "Not metni zorunludur.")]
    [StringLength(10000, ErrorMessage = "Not en fazla 10000 karakter olabilir.")]
    public string Text { get; set; } = string.Empty;
}
public sealed class CollectionCustomerNoteChange
{
    [StringLength(10000, ErrorMessage = "Not en fazla 10000 karakter olabilir.")]
    public string? Text { get; set; }
    [Required(ErrorMessage = "Kayıt sürümü zorunludur.")]
    [MinLength(8, ErrorMessage = "Kayıt sürümü geçersiz.")]
    [MaxLength(8, ErrorMessage = "Kayıt sürümü geçersiz.")]
    public byte[] RowVersion { get; set; } = [];
}
public sealed record CollectionCustomerPaymentItem(long Id, long ContractId, string ServiceTypeName,
    DateOnly Period, DateOnly PaymentDate, decimal Amount, string CurrencyCode, string? Description, bool IsFree);
