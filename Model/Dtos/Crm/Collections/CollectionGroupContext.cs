namespace Model.Dtos.Crm.Collections;

public sealed record CollectionGroupContext(long CustomerGroupId, string GroupCode, string GroupName,
    long? ParentCustomerId, string? ParentName, string? AccountNo, bool? IsCorporate, int MemberCount);
