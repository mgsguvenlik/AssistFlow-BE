using Model.Abstractions;
using Model.Interfaces;

namespace Model.Concrete.Collections;

/// <summary>Financial payment uses physical deletion, not ISoftDeletable.</summary>
public sealed class CollectionPayment : BaseEntity, ITimestamped, IAuditedByUser
{
    public long Id { get; set; }
    public long ContractId { get; set; }
    public CollectionContract Contract { get; set; } = null!;
    /// <summary>Accounting month represented by its first day; not the renewal date.</summary>
    public DateOnly Period { get; set; }
    public DateOnly PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public long CurrencyTypeId { get; set; }
    public CurrencyType CurrencyType { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsFree { get; set; }
    public DateTimeOffset CreatedDate { get; set; }
    public DateTimeOffset? UpdatedDate { get; set; }
    public long CreatedUser { get; set; }
    public long? UpdatedUser { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
