namespace Model.Dtos.Crm.Collections;

public sealed class CollectionBalanceQuery
{
    public DateOnly Period { get; init; }
    public DateOnly? AsOfDate { get; init; }
    public bool IncludeCarryOver { get; init; }
    /// <summary>Same scope as tracking: all due dates and posted payments of the selected accounting period.</summary>
    public bool FullPeriod { get; init; }
}

public sealed record CollectionPeriodBalance(DateOnly Period, DateOnly FromPeriod, DateOnly AsOfDate,
    bool IncludesCarryOver,
    IReadOnlyList<CollectionCurrencyBalance> Items, bool FullPeriod = false);

public sealed record CollectionCurrencyBalance(long CurrencyTypeId, string CurrencyCode,
    decimal AccruedAmount, decimal PaymentAmount, decimal RemainingAmount);
