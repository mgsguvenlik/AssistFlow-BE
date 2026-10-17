namespace Model.Dtos.Crm.Collections;

public sealed record CollectionInvoiceLoadItem(long Id, string Type, string FileName, DateTimeOffset CreatedDate,
    int Total, int Ready, int Errors, int Duplicates, int Imported, byte[] RowVersion, string? FileUrl);
public sealed record CollectionInvoiceLoadRowItem(long Id, int RowNumber, CollectionInvoiceFileRow Source,
    long? CustomerId, string? CustomerName, long? CurrencyTypeId, string Status, string? Issue, long? InvoiceId);
public sealed class CollectionInvoiceLoadApply { public byte[] RowVersion { get; set; } = []; }
