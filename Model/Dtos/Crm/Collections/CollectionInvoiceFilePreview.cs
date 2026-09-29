namespace Model.Dtos.Crm.Collections;

public sealed record CollectionInvoiceFileRow(int RowNumber, string AccountCode, string CustomerName,
    string Number, DateOnly? Date, decimal? Amount, string CurrencyCode, string PaidText,
    string Comment, string ProjectCode, IReadOnlyList<string> Errors);

// Parsed input only; no row is approved for persistence until customer/currency/duplicate checks run.
public sealed record CollectionInvoiceFilePreview(string FileHash, string Type,
    IReadOnlyList<CollectionInvoiceFileRow> Rows);
