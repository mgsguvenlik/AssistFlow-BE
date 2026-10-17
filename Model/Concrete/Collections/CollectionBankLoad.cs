using Model.Abstractions;

namespace Model.Concrete.Collections;

public sealed class CollectionBankLoad : BaseEntity
{
    public long Id { get; set; }
    public string Type { get; set; } = "";
    public string FileHash { get; set; } = "";
    public string FileName { get; set; } = "";
    public string StoredFileName { get; set; } = "";
    public DateOnly Period { get; set; }
    public long CreatedUser { get; set; }
    public DateTimeOffset CreatedDate { get; set; }
    public DateTimeOffset ReviewedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public ICollection<CollectionBankLoadRow> Rows { get; set; } = new List<CollectionBankLoadRow>();
}
public sealed class CollectionBankLoadRow : BaseEntity
{
    public long Id { get; set; }
    public long LoadId { get; set; }
    public CollectionBankLoad Load { get; set; } = null!;
    public int RowNumber { get; set; }
    public string SourceJson { get; set; } = "";
    public string TransactionKey { get; set; } = "";
    public string Status { get; set; } = "Review";
    public string? Issue { get; set; }
    public long? ContractId { get; set; }
    public long? CurrencyTypeId { get; set; }
    public long? PaymentId { get; set; }
    public long? AppliedUser { get; set; }
    public DateTimeOffset? AppliedAt { get; set; }
}
// No payment FK: deleting a financial entry must never release its bank identity.
public sealed class CollectionBankTransaction : BaseEntity
{
    public string Key { get; set; } = "";
    public string Source { get; set; } = "";
    public long? PaymentId { get; set; }
    public long? LoadRowId { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
}
public sealed class CollectionBankBaseline : BaseEntity
{
    public int Id { get; set; }
    public string SourceHash { get; set; } = "";
    public long SourceCount { get; set; }
    public DateTimeOffset VerifiedAt { get; set; }
}
