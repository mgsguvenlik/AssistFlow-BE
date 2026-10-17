using Model.Abstractions;

namespace Model.Concrete.Collections;

public sealed class CollectionInvoiceAccount : BaseEntity
{
    public string Code { get; set; } = "";
    public long? CustomerId { get; set; }
    public string? Issue { get; set; }
    public string SourceIdsJson { get; set; } = "[]";
    public string SourceHash { get; set; } = "";
    public DateTimeOffset VerifiedAt { get; set; }
}
public sealed class CollectionInvoiceLoad : BaseEntity
{
    public long Id { get; set; }
    public string Type { get; set; } = "";
    public string FileHash { get; set; } = "";
    public string FileName { get; set; } = "";
    public string StoredFileName { get; set; } = "";
    public long CreatedUser { get; set; }
    public DateTimeOffset CreatedDate { get; set; }
    public long? AppliedUser { get; set; }
    public DateTimeOffset? AppliedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public ICollection<CollectionInvoiceLoadRow> Rows { get; set; } = new List<CollectionInvoiceLoadRow>();
}
public sealed class CollectionInvoiceLoadRow : BaseEntity
{
    public long Id { get; set; }
    public long LoadId { get; set; }
    public CollectionInvoiceLoad Load { get; set; } = null!;
    public int RowNumber { get; set; }
    public string SourceJson { get; set; } = "";
    public long? CustomerId { get; set; }
    public long? CurrencyTypeId { get; set; }
    public string Status { get; set; } = "Error";
    public string? Issue { get; set; }
    public long? InvoiceId { get; set; }
    public long? AppliedUser { get; set; }
    public DateTimeOffset? AppliedAt { get; set; }
    // Retained even after financial deletion: a file must not resurrect the invoice.
    public string? ImportedKey { get; set; }
}
