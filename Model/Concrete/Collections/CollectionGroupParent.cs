using Model.Abstractions;

namespace Model.Concrete.Collections;

/// <summary>Legacy üst kart kimliği; grup tanımı ve üye müşteriler yeniden oluşturulmaz.</summary>
public sealed class CollectionGroupParent : BaseEntity
{
    public long CustomerId { get; set; }
    public long CustomerGroupId { get; set; }
    public long LegacyCustomerId { get; set; }
    public string? AccountNo { get; set; }
    public bool IsCorporate { get; set; }
    public byte[] SourceHash { get; set; } = [];
    public DateTimeOffset CreatedDate { get; set; }
    public long CreatedUser { get; set; }
}
