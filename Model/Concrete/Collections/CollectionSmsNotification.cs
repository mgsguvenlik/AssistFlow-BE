using Model.Abstractions;

namespace Model.Concrete.Collections;

public enum CollectionSmsStatus : byte { Pending, Sending, Accepted, Failed, Unknown, Simulation }

/// <summary>One durable notification per rate change. Rate id survives financial history removal.</summary>
public sealed class CollectionSmsNotification : BaseEntity
{
    public long Id { get; set; }
    public long ContractId { get; set; }
    public long RatePeriodId { get; set; }
    public decimal OldAmount { get; set; }
    public decimal NewAmount { get; set; }
    public decimal? IncreasePercent { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public CollectionSmsStatus Status { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset CreatedDate { get; set; }
    public DateTimeOffset UpdatedDate { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public ICollection<CollectionSmsAttempt> Attempts { get; set; } = new List<CollectionSmsAttempt>();
}

public sealed class CollectionSmsAttempt : BaseEntity
{
    public long Id { get; set; }
    public long NotificationId { get; set; }
    public CollectionSmsNotification Notification { get; set; } = null!;
    public int Sequence { get; set; }
    public long? RecipientCustomerId { get; set; }
    public string Phone { get; set; } = "";
    public string Message { get; set; } = "";
    public string Template { get; set; } = "";
    public string Reason { get; set; } = "";
    public bool IsAutomatic { get; set; }
    public bool IsSimulation { get; set; }
    public CollectionSmsStatus Status { get; set; }
    public string? ResultMessage { get; set; }
    public string? ErrorCode { get; set; }
    public string? PackageId { get; set; }
    public DateTimeOffset CreatedDate { get; set; }
    public long CreatedUser { get; set; }
    public DateTimeOffset? StartedDate { get; set; }
    public DateTimeOffset? CompletedDate { get; set; }
}
