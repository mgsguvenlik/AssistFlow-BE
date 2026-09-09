namespace Business.Services.Crm.Collections.Calculation;

/// <summary>
/// Calendar primitives for the verified legacy month-based schedule.
/// Does not decide eligibility, freezing, exchange rates or create financial records.
/// </summary>
public static class CollectionPeriodRules
{
    public static bool IsSupportedInterval(int intervalMonths) =>
        intervalMonths is 1 or 2 or 3 or 4 or 6 or 12 or 24 or 36;

    /// <summary>Yields due dates only inside the requested half-open window.</summary>
    public static IEnumerable<DateOnly> GetDueDates(
        DateOnly effectiveFrom,
        DateOnly? effectiveToExclusive,
        int intervalMonths,
        DateOnly windowFrom,
        DateOnly windowToExclusive)
    {
        if (effectiveFrom.Day != 1)
            throw new ArgumentException("Legacy rate periods start on the first day of a month.", nameof(effectiveFrom));
        if (!IsSupportedInterval(intervalMonths))
            throw new ArgumentOutOfRangeException(nameof(intervalMonths));
        if (effectiveToExclusive.HasValue && effectiveToExclusive.Value <= effectiveFrom)
            throw new ArgumentException("The end must follow the start.", nameof(effectiveToExclusive));
        if (windowToExclusive <= windowFrom)
            throw new ArgumentException("The query window must be non-empty.", nameof(windowToExclusive));

        var upper = effectiveToExclusive.HasValue && effectiveToExclusive.Value < windowToExclusive
            ? effectiveToExclusive.Value : windowToExclusive;
        var lower = windowFrom > effectiveFrom ? windowFrom : effectiveFrom;
        if (lower >= upper)
            return Array.Empty<DateOnly>();

        // Jump straight to the requested window, preserving the original period anchor.
        var anchor = MonthIndex(effectiveFrom);
        var difference = MonthIndex(lower) - anchor;
        var first = anchor + ((difference + intervalMonths - 1) / intervalMonths) * intervalMonths;
        return Enumerate(first, intervalMonths, lower, upper);
    }

    private static IEnumerable<DateOnly> Enumerate(int monthIndex, int interval, DateOnly lower, DateOnly upper)
    {
        const int lastMonthIndex = 9999 * 12 - 1;
        while (monthIndex <= lastMonthIndex)
        {
            var candidate = new DateOnly(monthIndex / 12 + 1, monthIndex % 12 + 1, 1);
            if (candidate >= upper)
                yield break;
            if (candidate >= lower)
                yield return candidate;
            monthIndex += interval;
        }
    }

    private static int MonthIndex(DateOnly date) => (date.Year - 1) * 12 + date.Month - 1;

    /// <summary>Preserves a legacy inclusive end date; an unrepresentable end is an exception.</summary>
    public static DateOnly? ToExclusiveEnd(DateOnly? inclusiveEnd)
    {
        if (!inclusiveEnd.HasValue)
            return null;
        if (inclusiveEnd.Value == DateOnly.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(inclusiveEnd), "An exclusive end cannot represent this source date.");
        return inclusiveEnd.Value.AddDays(1);
    }

    /// <summary>
    /// Finds one overlap witness for each conflicting row in a single contract.
    /// Call separately per contract. Includes nested and open-ended intervals.
    /// </summary>
    public static IReadOnlyList<RatePeriodOverlap> FindOverlaps(IEnumerable<RatePeriodRange> periods)
    {
        ArgumentNullException.ThrowIfNull(periods);
        var ordered = periods.OrderBy(p => p.EffectiveFrom).ThenBy(p => p.Id).ToArray();
        var identifiers = new HashSet<long>();
        foreach (var period in ordered)
        {
            if (period.Id <= 0 || !identifiers.Add(period.Id))
                throw new ArgumentException("Each source row needs a distinct positive identifier.", nameof(periods));
            if (period.EffectiveToExclusive.HasValue && period.EffectiveToExclusive.Value <= period.EffectiveFrom)
                throw new ArgumentException("Reversed or empty ranges require separate validation.", nameof(periods));
        }

        var overlaps = new List<RatePeriodOverlap>();
        RatePeriodRange? furthest = null;
        foreach (var period in ordered)
        {
            if (furthest is not null &&
                (!furthest.EffectiveToExclusive.HasValue || period.EffectiveFrom < furthest.EffectiveToExclusive.Value))
                overlaps.Add(new RatePeriodOverlap(furthest.Id, period.Id));

            if (furthest is null ||
                (furthest.EffectiveToExclusive.HasValue &&
                 (!period.EffectiveToExclusive.HasValue || period.EffectiveToExclusive.Value > furthest.EffectiveToExclusive.Value)))
                furthest = period;
        }
        return overlaps;
    }
}

public sealed record RatePeriodRange(long Id, DateOnly EffectiveFrom, DateOnly? EffectiveToExclusive);
public sealed record RatePeriodOverlap(long EarlierRowId, long ConflictingRowId);
