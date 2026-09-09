using Model.Abstractions;

namespace Model.Concrete.Collections;

/// <summary>
/// Contract identity draft. Financial terms and pending operational definitions are separate work.
/// Not registered in AppDataContext until the complete model has been reviewed.
/// </summary>
public sealed class CollectionContract : AuditableWithUserEntity
{
    public long Id { get; set; }
    public long CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public long ServiceTypeId { get; set; }
    public ServiceType ServiceType { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    /// <summary>Inclusive source end date; no billing eligibility is inferred from this value.</summary>
    public DateOnly? EndDate { get; set; }
    public string? GtsNo { get; set; }
    public string? IvrNo { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
