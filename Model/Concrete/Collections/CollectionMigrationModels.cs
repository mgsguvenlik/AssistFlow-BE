using Model.Abstractions;

namespace Model.Concrete.Collections;

public enum CollectionMigrationBatchStatus : byte { Staged, Validating, Validated, Applying, Reconciled, Failed, NeedsReview }
public enum CollectionMigrationRowStatus : byte { Pending, Ready, Blocked, Applied, AlreadyApplied, Excluded }
public enum CollectionMigrationDecisionStatus : byte { Pending, Accepted, Rejected }
public enum CollectionMigrationIssueSeverity : byte { Warning, Blocker }
public enum CollectionMigrationIssueStatus : byte { Open, Resolved, Ignored }

public sealed class CollectionMigrationBatch : BaseEntity
{
    public long Id { get; set; }
    public string SourceSystem { get; set; } = null!;
    public string SnapshotKey { get; set; } = null!;
    public byte[] ManifestHash { get; set; } = [];
    public string RuleVersion { get; set; } = null!;
    public string NormalizationVersion { get; set; } = null!;
    public CollectionMigrationBatchStatus Status { get; set; }
    public DateTimeOffset CreatedDate { get; set; }
    public long CreatedUser { get; set; }
    public DateTimeOffset? CompletedDate { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class CollectionMigrationSourceRow : BaseEntity
{
    public long Id { get; set; }
    public long BatchId { get; set; }
    public CollectionMigrationBatch Batch { get; set; } = null!;
    public string EntityCode { get; set; } = null!;
    public string SourceId { get; set; } = null!;
    public string? SourceParentId { get; set; }
    public string Payload { get; set; } = null!;
    public byte[] PayloadHash { get; set; } = [];
    public DateTimeOffset StagedDate { get; set; }
}

public sealed class CollectionMigrationContractStage : BaseEntity
{
    public long SourceRowId { get; set; }
    public CollectionMigrationSourceRow SourceRow { get; set; } = null!;
    public string? SourceCustomerId { get; set; }
    public string? SubscriberNoRaw { get; set; }
    public string? SubscriberNoNormalized { get; set; }
    public string? SourceServiceTypeId { get; set; }
    public string? SourceContractStatusId { get; set; }
    public string? SourceSubscriptionStatusId { get; set; }
    public string? SourcePaymentMethodId { get; set; }
    public short? StartingMonth { get; set; }
    public short? StartingYear { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? GtsNo { get; set; }
    public string? IvrNo { get; set; }
    public string? AttachmentName { get; set; }
    public string? AttachmentPath { get; set; }
    public long? TargetCustomerId { get; set; }
    public Customer? TargetCustomer { get; set; }
    public long? TargetServiceTypeId { get; set; }
    public ServiceType? TargetServiceType { get; set; }
    public CollectionMigrationRowStatus Status { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class CollectionMigrationRatePeriodStage : BaseEntity
{
    public long SourceRowId { get; set; }
    public CollectionMigrationSourceRow SourceRow { get; set; } = null!;
    public string? SourceContractId { get; set; }
    public string? SourceCustomerId { get; set; }
    public DateOnly? EffectiveFrom { get; set; }
    public DateOnly? EffectiveToExclusive { get; set; }
    public decimal? Amount { get; set; }
    public string? SourceCurrencyId { get; set; }
    public string? SourcePaymentTypeId { get; set; }
    public string? ProcessType { get; set; }
    public string? Description { get; set; }
    public long? TargetCurrencyTypeId { get; set; }
    public CurrencyType? TargetCurrencyType { get; set; }
    public long? TargetPaymentFrequencyId { get; set; }
    public CollectionPaymentFrequency? TargetPaymentFrequency { get; set; }
    public CollectionBillingBehavior? BillingBehavior { get; set; }
    public CollectionMigrationRowStatus Status { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class CollectionMigrationReferenceMap : BaseEntity
{
    public long Id { get; set; }
    public long BatchId { get; set; }
    public CollectionMigrationBatch Batch { get; set; } = null!;
    public string ReferenceKind { get; set; } = null!;
    public string SourceId { get; set; } = null!;
    public long? TargetCustomerId { get; set; }
    public Customer? TargetCustomer { get; set; }
    public long? TargetCustomerTypeId { get; set; }
    public CustomerType? TargetCustomerType { get; set; }
    public long? TargetServiceTypeId { get; set; }
    public ServiceType? TargetServiceType { get; set; }
    public long? TargetCurrencyTypeId { get; set; }
    public CurrencyType? TargetCurrencyType { get; set; }
    public long? TargetPaymentFrequencyId { get; set; }
    public CollectionPaymentFrequency? TargetPaymentFrequency { get; set; }
    public long? TargetPaymentMethodId { get; set; }
    public CollectionPaymentMethod? TargetPaymentMethod { get; set; }
    public long? TargetSubscriptionStatusId { get; set; }
    public CollectionSubscriptionStatus? TargetSubscriptionStatus { get; set; }
    public long? TargetContractStatusId { get; set; }
    public CollectionContractStatus? TargetContractStatus { get; set; }
    public string MatchMethod { get; set; } = null!;
    public string? Evidence { get; set; }
    public CollectionMigrationDecisionStatus Status { get; set; }
    public long? DecidedUser { get; set; }
    public DateTimeOffset? DecidedDate { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class CollectionMigrationIssue : BaseEntity
{
    public long Id { get; set; }
    public long SourceRowId { get; set; }
    public CollectionMigrationSourceRow SourceRow { get; set; } = null!;
    public string IssueCode { get; set; } = null!;
    public CollectionMigrationIssueSeverity Severity { get; set; }
    public CollectionMigrationIssueStatus Status { get; set; }
    public string? Details { get; set; }
    public string? ResolutionNote { get; set; }
    public string RuleVersion { get; set; } = null!;
    public DateTimeOffset CreatedDate { get; set; }
    public long? ResolvedUser { get; set; }
    public DateTimeOffset? ResolvedDate { get; set; }
}

public sealed class CollectionMigrationMap : BaseEntity
{
    public long Id { get; set; }
    public string SourceSystem { get; set; } = null!;
    public string EntityCode { get; set; } = null!;
    public string SourceId { get; set; } = null!;
    public long? TargetContractId { get; set; }
    public CollectionContract? TargetContract { get; set; }
    public long? TargetRatePeriodId { get; set; }
    public CollectionContractRatePeriod? TargetRatePeriod { get; set; }
    public long? TargetPaymentId { get; set; }
    public CollectionPayment? TargetPayment { get; set; }
    public byte[] AppliedPayloadHash { get; set; } = [];
    public long FirstBatchId { get; set; }
    public CollectionMigrationBatch FirstBatch { get; set; } = null!;
    public long LastBatchId { get; set; }
    public CollectionMigrationBatch LastBatch { get; set; } = null!;
    public DateTimeOffset CreatedDate { get; set; }
    public DateTimeOffset LastSeenDate { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
