namespace Model.Dtos.Crm.Collections;

public sealed class CollectionBalanceQuery
{
    public DateOnly Period { get; init; }
    public DateOnly? AsOfDate { get; init; }
    public bool IncludeCarryOver { get; init; }
}

public sealed record CollectionPeriodBalance(DateOnly Period, DateOnly FromPeriod, DateOnly AsOfDate,
    bool IncludesCarryOver,
    IReadOnlyList<CollectionCurrencyBalance> Items);

public sealed record CollectionCurrencyBalance(long CurrencyTypeId, string CurrencyCode,
    decimal AccruedAmount, decimal PaymentAmount, decimal RemainingAmount);
