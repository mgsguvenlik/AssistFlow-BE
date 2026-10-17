namespace Model.Dtos.Crm.Collections;

public sealed record CollectionContractAttachmentItem(long Id, string OriginalFileName, string Url,
    string ContentType, long SizeBytes, DateTimeOffset CreatedDate);
