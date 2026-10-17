namespace Model.Dtos.Crm.Collections;

public sealed class CollectionInvoiceQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public long? CustomerId { get; set; }
    public string? Type { get; set; }
    public string? Search { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public bool? Paid { get; set; }
}

public sealed record CollectionInvoiceItem(long Id, long CustomerId, string? CustomerName, string? SubscriberCode,
    string Type, string? Number, DateOnly Date, decimal Amount, string CurrencyCode,
    decimal PaymentAmount, decimal RemainingAmount, string? ProjectCode);
public sealed record CollectionInvoiceDetail(CollectionInvoiceItem Invoice, string? Comment, byte[] RowVersion, long? LegacyInvoiceFollowId);
public sealed record CollectionInvoicePaymentItem(long Id, DateOnly Date, decimal Amount, string? Description, byte[] RowVersion, long? LegacyInvoiceFollowPaymentId);
