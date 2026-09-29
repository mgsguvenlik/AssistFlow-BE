namespace Model.Dtos.Crm.Collections;

// Deliberately excludes card, expiry, address and contact fields from the source workbook.
public sealed record CollectionBankFileRow(int RowNumber, string TransactionNumber,
    string ContractReference, DateTime? PaymentDate, string ProcessType, decimal? Amount,
    string CurrencyCode, string BankResult, string Status, IReadOnlyList<string> Issues);

// Candidate means format validation only, never permission to create a payment.
public sealed record CollectionBankFilePreview(string FileHash, string Type, DateOnly Period,
    IReadOnlyList<CollectionBankFileRow> Rows);
