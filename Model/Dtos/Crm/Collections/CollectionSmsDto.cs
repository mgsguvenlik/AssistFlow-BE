using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.Crm.Collections;

public sealed record CollectionSmsPreview(long RatePeriodId, string? RecipientName, string? Phone,
    string Message, DateOnly EffectiveDate, decimal OldAmount, decimal NewAmount, decimal? IncreasePercent,
    string PreviewHash, bool IsSimulation, bool CanSend, string? BlockReason);

public sealed class CollectionSmsSendCommand
{
    [Range(1, long.MaxValue, ErrorMessage = "Geçerli tarife kimliği gereklidir.")]
    public long RatePeriodId { get; set; }
    [Required(ErrorMessage = "Mesaj önizlemesi gereklidir.")]
    [RegularExpression("^[A-F0-9]{64}$", ErrorMessage = "Mesaj önizlemesi geçersiz.")]
    public string PreviewHash { get; set; } = "";
    [Required(ErrorMessage = "Gönderim nedeni gereklidir.")]
    [StringLength(500, MinimumLength = 3, ErrorMessage = "Gönderim nedeni 3–500 karakter olmalıdır.")]
    public string Reason { get; set; } = "";
}

public sealed record CollectionSmsHistoryItem(long Id, long NotificationId, long RatePeriodId, int Sequence,
    DateTimeOffset CreatedDate, DateTimeOffset? CompletedDate, long CreatedUser, string Phone, string Message,
    string Reason, bool IsAutomatic, bool IsSimulation, byte Status, string? ResultMessage, string? ErrorCode,
    string? PackageId, DateOnly EffectiveDate, decimal OldAmount, decimal NewAmount);
public sealed record CollectionSmsHistory(IReadOnlyList<CollectionSmsHistoryItem> Items, int TotalCount);

public sealed class CollectionSmsTrackingQuery
{
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public byte? Status { get; set; }
    public bool LatestOnly { get; set; } = true;
    public string? Search { get; set; }
}
public sealed record CollectionSmsTrackingItem(long ContractId, string? SubscriberCode, string? CustomerName,
    string? ServiceName, bool ContractExists, CollectionSmsHistoryItem Attempt);
public sealed record CollectionSmsStatusCount(byte Status, int Count);
public sealed record CollectionSmsTracking(IReadOnlyList<CollectionSmsTrackingItem> Items, int TotalCount,
    IReadOnlyList<CollectionSmsStatusCount> Counts);
