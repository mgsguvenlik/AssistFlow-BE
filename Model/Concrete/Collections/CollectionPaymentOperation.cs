using Model.Abstractions;

namespace Model.Concrete.Collections;

/// <summary>Committed receipt and audit. Retained after physical deletion of the referenced payment.</summary>
public sealed class CollectionPaymentOperation : BaseEntity
{
    public long Id { get; set; }
    public Guid RequestId { get; set; }
    public long ActorUserId { get; set; }
    public CollectionPaymentOperationKind Kind { get; set; }
    public byte[] PayloadHash { get; set; } = Array.Empty<byte>();
    // Deliberately no payment FK: financial deletion must not delete its audit/receipt.
    public long PaymentId { get; set; }
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public DateTimeOffset CompletedDate { get; set; }
}

public enum CollectionPaymentOperationKind : byte { Create, Update, Delete }
