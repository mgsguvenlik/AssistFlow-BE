using Model.Abstractions;
namespace Model.Concrete.Collections;

// No FK: deletion must retain the correction receipt and before/after evidence.
public sealed class CollectionContractCorrection : BaseEntity
{
    public long Id { get; set; }
    public long ContractId { get; set; }
    public Guid RequestId { get; set; }
    public string PayloadHash { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Reason { get; set; } = "";
    public string BeforeJson { get; set; } = "";
    public string AfterJson { get; set; } = "";
    public long ActorUserId { get; set; }
    public DateTimeOffset CreatedDate { get; set; }
}
