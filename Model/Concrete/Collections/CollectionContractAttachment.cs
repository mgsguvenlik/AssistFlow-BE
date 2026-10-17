using Model.Abstractions;

namespace Model.Concrete.Collections;

public sealed class CollectionContractAttachment : AuditableWithUserEntity
{
    public long Id { get; set; }
    public long ContractId { get; set; }
    public CollectionContract Contract { get; set; } = null!;
    public string OriginalFileName { get; set; } = null!;
    public string StoredFileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public long SizeBytes { get; set; }
}
