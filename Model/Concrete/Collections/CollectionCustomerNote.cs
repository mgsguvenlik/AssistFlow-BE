using Model.Abstractions;

namespace Model.Concrete.Collections;

public sealed class CollectionCustomerNote : AuditableWithUserEntity
{
    public long Id { get; set; }
    public long CustomerId { get; set; }
    public string Text { get; set; } = string.Empty;
    public byte[] RowVersion { get; set; } = [];
    public long? LegacyCommentId { get; set; }
    public long? LegacyCustomerId { get; set; }
    public string? LegacyCreatedBy { get; set; }
    public string? LegacyModifiedBy { get; set; }
    public DateTime? LegacyCreatedOn { get; set; }
    public DateTime? LegacyModifiedOn { get; set; }
    public byte[]? SourceHash { get; set; }
}

