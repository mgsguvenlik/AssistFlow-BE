using Model.Concrete.Collections;

namespace Business.Services.Crm.Collections.Calculation;

/// <summary>
/// Calculates proposed charges for one validated contract timeline. Never persists or changes payments.
/// Caller supplies the complete effective timeline; an absent timeline is not proof of zero debt.
/// </summary>
public static class CollectionAccrualRules
{
    public static IReadOnlyList<CollectionCharge> Calculate(
        IEnumerable<CollectionRateSegment> segments, DateOnly windowFrom, DateOnly windowToExclusive)
    {
        ArgumentNullException.ThrowIfNull(segments);
        if (windowToExclusive <= windowFrom) throw new ArgumentException("Geçerli bir sorgu tarih aralığı gereklidir.");
        var ordered = segments.OrderBy(x => x.EffectiveFrom).ThenBy(x => x.Id).ToArray();
        if (ordered.Length == 0) throw new ArgumentException("Doğrulanmış tarife geçmişi gereklidir.", nameof(segments));
        foreach (var segment in ordered)
        {
            if (!Enum.IsDefined(segment.Behavior) || !CollectionPeriodRules.IsSupportedInterval(segment.IntervalMonths))
                throw new ArgumentException("Tahakkuk davranışı veya ödeme sıklığı geçersiz.", nameof(segments));
            if (segment.BillingAnchor > segment.EffectiveFrom)
                throw new ArgumentException("Yenileme başlangıcı tarife başlangıcından sonra olamaz.", nameof(segments));
            if (segment.Amount is < 0 || segment.CurrencyTypeId is <= 0)
                throw new ArgumentException("Tutar veya para birimi geçersiz.", nameof(segments));
            if (segment.Behavior == CollectionBillingBehavior.Billable && (segment.Amount is null || segment.CurrencyTypeId is null))
                throw new ArgumentException("Ücretli dönemlerde tutar ve eşlenmiş para birimi gereklidir.", nameof(segments));
        }
        if (CollectionPeriodRules.FindOverlaps(ordered.Select(x => new RatePeriodRange(x.Id, x.EffectiveFrom, x.EffectiveToExclusive))).Count != 0)
            throw new ArgumentException("Çakışan tarifeler düzeltilmeden hesaplama yapılamaz.", nameof(segments));
        for (var index = 1; index < ordered.Length; index++)
            if (ordered[index - 1].EffectiveToExclusive != ordered[index].EffectiveFrom)
                throw new ArgumentException("Tarife geçmişindeki boşluklar giderilmelidir.", nameof(segments));

        var charges = new List<CollectionCharge>();
        foreach (var segment in ordered)
        {
            if (segment.Behavior != CollectionBillingBehavior.Billable) continue;
            var lower = segment.EffectiveFrom > windowFrom ? segment.EffectiveFrom : windowFrom;
            if (lower >= windowToExclusive) continue;
            foreach (var dueDate in CollectionPeriodRules.GetDueDates(segment.BillingAnchor,
                segment.EffectiveToExclusive, segment.IntervalMonths, lower, windowToExclusive))
                charges.Add(new(segment.Id, dueDate, segment.Amount!.Value, segment.CurrencyTypeId!.Value));
        }
        return charges;
    }
}

/// <summary>BillingAnchor changes on reactivation, not on an ordinary price increase.</summary>
public sealed record CollectionRateSegment(long Id, DateOnly EffectiveFrom, DateOnly? EffectiveToExclusive,
    DateOnly BillingAnchor, int IntervalMonths, decimal? Amount, long? CurrencyTypeId, CollectionBillingBehavior Behavior);

public sealed record CollectionCharge(long RateSegmentId, DateOnly DueDate, decimal Amount, long CurrencyTypeId);
