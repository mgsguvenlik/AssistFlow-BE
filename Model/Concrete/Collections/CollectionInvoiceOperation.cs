using Model.Abstractions;

namespace Model.Concrete.Collections;

// No financial FK: the receipt must survive physical deletion.
public sealed class CollectionInvoiceOperation : BaseEntity
{
    public long Id { get; set; }
    public Guid RequestId { get; set; }
    public long ActorUserId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public long InvoiceId { get; set; }
    public long? PaymentId { get; set; }
    public long? LegacyInvoiceId { get; set; }
    public long? LegacyPaymentId { get; set; }
    public byte[] PayloadHash { get; set; } = [];
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public DateTimeOffset CompletedDate { get; set; }
}
