using Model.Abstractions;

namespace Model.Concrete.Collections;

/// <summary>
/// Contract identity and creation receipt. Financial terms are stored in effective rate periods.
/// </summary>
public sealed class CollectionContract : AuditableWithUserEntity
{
    public long Id { get; set; }
    public Guid? CreationRequestId { get; set; }
    public byte[]? CreationPayloadHash { get; set; }
    public long CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public long ServiceTypeId { get; set; }
    public ServiceType ServiceType { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    /// <summary>Inclusive source end date; no billing eligibility is inferred from this value.</summary>
    public DateOnly? EndDate { get; set; }
    public string? GtsNo { get; set; }
    public string? IvrNo { get; set; }
    public long? SubscriptionStatusId { get; set; }
    public CollectionSubscriptionStatus? SubscriptionStatus { get; set; }
    public long? ContractStatusId { get; set; }
    public CollectionContractStatus? ContractStatus { get; set; }
    public long? PaymentMethodId { get; set; }
    public CollectionPaymentMethod? PaymentMethod { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
