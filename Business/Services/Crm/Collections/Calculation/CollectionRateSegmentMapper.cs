using Model.Concrete.Collections;

namespace Business.Services.Crm.Collections.Calculation;

public static class CollectionRateSegmentMapper
{
    /// <summary>Use the persisted frequency's interval, not a guessed default or current contract price.</summary>
    public static CollectionRateSegment FromPersisted(CollectionContractRatePeriod rate, int intervalMonths)
    {
        ArgumentNullException.ThrowIfNull(rate);
        if (!CollectionPeriodRules.IsSupportedInterval(intervalMonths))
            throw new ArgumentException("Tarifenin ödeme dönemi geçersiz.", nameof(intervalMonths));
        return new(rate.Id, rate.EffectiveFrom, rate.EffectiveToExclusive, rate.BillingAnchor,
            intervalMonths, rate.Amount, rate.CurrencyTypeId, rate.BillingBehavior, rate.OriginalAnchorDay);
    }
}
