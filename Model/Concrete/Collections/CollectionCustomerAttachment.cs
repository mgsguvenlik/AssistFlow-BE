using Model.Abstractions;
namespace Model.Concrete.Collections;

public sealed class CollectionCustomerAttachment : BaseEntity
{
    public long Id { get; set; }
    public long CustomerId { get; set; }
    public long? LegacyCustomerId { get; set; }
    public long? RetainedLegacyCustomerId { get; set; }
    public string SourcePath { get; set; } = "";
    public string? ArchiveHash { get; set; }
    public string ContentHash { get; set; } = "";
    public string OriginalFileName { get; set; } = "";
    public string StoredFileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public string Decision { get; set; } = "";
    public DateTimeOffset CreatedDate { get; set; }
    public long? CreatedUser { get; set; }
    public bool IsDeleted { get; set; }
    public long? RemovedUser { get; set; }
    public DateTimeOffset? RemovedDate { get; set; }
}
