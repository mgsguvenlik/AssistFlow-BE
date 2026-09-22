using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.Crm.Collections;

public sealed class CollectionPaymentQuery
{
    [Range(1, 1000000, ErrorMessage = "Sayfa numarası 1 ile 1000000 arasında olmalıdır.")]
    public int Page { get; set; } = 1;
    [Range(1, 100, ErrorMessage = "Sayfa boyutu 1 ile 100 arasında olmalıdır.")]
    public int PageSize { get; set; } = 25;
}

public sealed class CollectionPaymentItem
{
    public long Id { get; init; }
    public DateOnly Period { get; init; }
    public DateOnly PaymentDate { get; init; }
    public decimal Amount { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public long CurrencyTypeId { get; init; }
    public string? Description { get; init; }
    public bool IsFree { get; init; }
    public byte[] RowVersion { get; init; } = [];
}
