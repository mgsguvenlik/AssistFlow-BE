namespace Model.Dtos.Crm.Collections;

public sealed class CollectionInvoiceCommand
{
    public Guid RequestId { get; set; }
    public byte[] InvoiceRowVersion { get; set; } = [];
    public byte[]? PaymentRowVersion { get; set; }
    public DateOnly? Date { get; set; }
    public decimal? Amount { get; set; }
    public string? Description { get; set; }
}
public sealed record CollectionInvoiceCommit(long InvoiceId, long? PaymentId, bool Replayed);
