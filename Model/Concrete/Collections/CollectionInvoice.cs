using Model.Abstractions;

namespace Model.Concrete.Collections;

// Issued invoices are independent from contract accruals and contract payments.
public sealed class CollectionInvoice : BaseEntity
{
    public long Id { get; set; }
    public long CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public string Type { get; set; } = string.Empty;
    public string? Number { get; set; }
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public long CurrencyTypeId { get; set; }
    public CurrencyType CurrencyType { get; set; } = null!;
    public string? ProjectCode { get; set; }
    public string? Comment { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public DateTimeOffset CreatedDate { get; set; }
    public long CreatedUser { get; set; }
    public long? LegacyInvoiceFollowId { get; set; }
    public long? LegacyCustomerId { get; set; }
    public string? LegacyCreatedBy { get; set; }
    public string? LegacyModifiedBy { get; set; }
    public DateTime? LegacyCreatedOn { get; set; }
    public DateTime? LegacyModifiedOn { get; set; }
    public byte[]? SourceHash { get; set; }
    public ICollection<CollectionInvoicePayment> Payments { get; set; } = new List<CollectionInvoicePayment>();
}
