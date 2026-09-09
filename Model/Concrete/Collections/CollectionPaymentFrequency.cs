using Model.Abstractions;

namespace Model.Concrete.Collections;

/// <summary>Month-based frequency definition. Existing references remain valid when inactive.</summary>
public sealed class CollectionPaymentFrequency : BaseEntity
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public short IntervalMonths { get; set; }
    public short DisplayOrder { get; set; }
    public bool IsActive { get; set; }
}
