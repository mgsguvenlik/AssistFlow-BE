using Model.Abstractions;

namespace Model.Concrete.Collections;

public sealed class CollectionContractRatePeriod : AuditableWithUserEntity
{
    public long Id { get; set; }
    public long ContractId { get; set; }
    public CollectionContract Contract { get; set; } = null!;
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveToExclusive { get; set; }
    public DateOnly BillingAnchor { get; set; }
    public long PaymentFrequencyId { get; set; }
    public CollectionPaymentFrequency PaymentFrequency { get; set; } = null!;
    public decimal? Amount { get; set; }
    public long? CurrencyTypeId { get; set; }
    public CurrencyType? CurrencyType { get; set; }
    public CollectionBillingBehavior BillingBehavior { get; set; }
    public string? ChangeReason { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
