using Model.Concrete.Collections;

namespace Model.Dtos.Crm.Collections;

/// <summary>Typed command content; actor and request key are supplied separately by the command boundary.</summary>
public sealed record CollectionPaymentCommand(
    CollectionPaymentOperationKind Kind,
    long? PaymentId,
    string? ExpectedRowVersion,
    long? ContractId,
    DateOnly? Period,
    DateOnly? PaymentDate,
    decimal? Amount,
    long? CurrencyTypeId,
    string? Description,
    bool? IsFree);
