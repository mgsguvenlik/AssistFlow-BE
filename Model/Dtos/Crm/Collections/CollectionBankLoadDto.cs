namespace Model.Dtos.Crm.Collections;

public sealed record CollectionBankLoadItem(long Id, string Type, string FileName, DateOnly Period,
    DateTimeOffset CreatedDate, int Total, int Ready, int Review, int BankFailed, int Duplicate, int Imported,
    byte[] RowVersion, string? FileUrl);
public sealed record CollectionBankLoadRowItem(long Id, int RowNumber, CollectionBankFileRow Source,
    long? ContractId, long? CurrencyTypeId, string Status, string? Issue, long? PaymentId);
public sealed record CollectionBankLoadApply(string RowVersion);
