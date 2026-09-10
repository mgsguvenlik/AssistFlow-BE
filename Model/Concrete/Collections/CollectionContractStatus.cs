using Model.Abstractions;

namespace Model.Concrete.Collections;

public sealed class CollectionContractStatus : BaseEntity
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
