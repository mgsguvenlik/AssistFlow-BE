using Model.Abstractions;

namespace Model.Concrete.Collections;

/// <summary>Operational label, independent of financial payment state.</summary>
public sealed class CollectionContractPeriodFollowUp : AuditableWithUserEntity
{
    public long Id { get; set; }
    public long ContractId { get; set; }
    public CollectionContract Contract { get; set; } = null!;
    public DateOnly Period { get; set; }
    public long? GroupStatusId { get; set; }
    public CollectionGroupStatus? GroupStatus { get; set; }
    public string? Description { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
