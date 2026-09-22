using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.Crm.Collections;

public sealed class CollectionPaymentCreate
{
    public Guid RequestId { get; init; }
    public DateOnly Period { get; init; }
    public DateOnly PaymentDate { get; init; }
    public decimal Amount { get; init; }
    [Range(1, long.MaxValue, ErrorMessage = "Para birimi seçilmelidir.")]
    public long CurrencyTypeId { get; init; }
    [StringLength(1000, ErrorMessage = "Açıklama en fazla 1000 karakter olabilir.")]
    public string? Description { get; init; }
}

public sealed class CollectionPaymentUpdate
{
    public Guid RequestId { get; init; }
    [Required(ErrorMessage = "Ödeme kayıt sürümü gereklidir.")]
    public string RowVersion { get; init; } = string.Empty;
    public DateOnly Period { get; init; }
    public DateOnly PaymentDate { get; init; }
    public decimal Amount { get; init; }
    [Range(1, long.MaxValue, ErrorMessage = "Para birimi seçilmelidir.")]
    public long CurrencyTypeId { get; init; }
    [StringLength(1000, ErrorMessage = "Açıklama en fazla 1000 karakter olabilir.")]
    public string? Description { get; init; }
}

public sealed class CollectionPaymentDelete
{
    public Guid RequestId { get; init; }
    [Required(ErrorMessage = "Ödeme kayıt sürümü gereklidir.")]
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class CollectionPaymentBatchCreate
{
    public Guid RequestId { get; init; }
    public DateOnly PaymentDate { get; init; }
    [StringLength(1000, ErrorMessage = "Açıklama en fazla 1000 karakter olabilir.")]
    public string? Description { get; init; }
    [Required(ErrorMessage = "Tahsil edilecek kayıtlar seçilmelidir.")]
    [MinLength(1, ErrorMessage = "En az bir tahsilat kaydı seçilmelidir.")]
    [MaxLength(50, ErrorMessage = "Tek seferde en fazla 50 tahsilat kaydı seçilebilir.")]
    public IReadOnlyList<CollectionPaymentBatchItem> Items { get; init; } = [];
}

public sealed class CollectionPaymentBatchItem
{
    [Range(1, long.MaxValue, ErrorMessage = "Geçerli sözleşme seçilmelidir.")]
    public long ContractId { get; init; }
    public DateOnly Period { get; init; }
    [Range(1, long.MaxValue, ErrorMessage = "Para birimi seçilmelidir.")]
    public long CurrencyTypeId { get; init; }
    public decimal ExpectedRemainingAmount { get; init; }
}

public sealed record CollectionPaymentBatchFailure(long ContractId, string Message);

public sealed record CollectionPaymentBatchResult(int CompletedCount, int ReplayedCount,
    IReadOnlyList<CollectionPaymentBatchFailure> Failures);
