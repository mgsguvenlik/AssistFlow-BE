namespace Model.Dtos.Crm.Collections;
public sealed record CollectionContractCorrectionCommand(Guid RequestId, string RowVersion, string Kind, string Reason,
    bool ConfirmImpact, DateOnly? StartDate, DateOnly? EndDate, long? PaymentMethodId, long? RateId,
    string? RateRowVersion, decimal? Amount, long? CurrencyTypeId, long? PaymentFrequencyId,
    bool CorrectRateDates = false, DateOnly? RateEffectiveFrom = null, DateOnly? RateEffectiveToExclusive = null);
public sealed record CollectionContractCorrectionItem(long Id, string Kind, string Reason, long ActorUserId,
    DateTimeOffset CreatedDate, string BeforeJson, string AfterJson);
