using Model.Abstractions;

namespace Model.Concrete.Collections;

// Currency and customer are owned by the invoice; no link to collection.Payment.
public sealed class CollectionInvoicePayment : BaseEntity
{
    public long Id { get; set; }
    public long InvoiceId { get; set; }
    public CollectionInvoice Invoice { get; set; } = null!;
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public DateTimeOffset CreatedDate { get; set; }
    public long CreatedUser { get; set; }
    public long? LegacyInvoiceFollowPaymentId { get; set; }
    public long? LegacyCustomerId { get; set; }
    public string? LegacyCreatedBy { get; set; }
    public string? LegacyModifiedBy { get; set; }
    public DateTime? LegacyCreatedOn { get; set; }
    public DateTime? LegacyModifiedOn { get; set; }
    public byte[]? SourceHash { get; set; }
}
