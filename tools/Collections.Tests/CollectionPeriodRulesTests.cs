using Business.Services.Crm.Collections.Calculation;
using Model.Concrete.Collections;
using Business.Services.Crm.Collections.Queries;
using Model.Dtos.Crm.Collections;
using System.ComponentModel.DataAnnotations;

var passed = 0;
DateOnly D(int year, int month, int day = 1) => new(year, month, day);
void Check(string name, Action action)
{
    action();
    passed++;
    Console.WriteLine($"PASS: {name}");
}
void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"Expected {expected}, got {actual}");
}
void Dates(DateOnly[] expected, IEnumerable<DateOnly> actual)
{
    if (!expected.SequenceEqual(actual))
        throw new InvalidOperationException("Due dates differ from the expected schedule.");
}
void Throws<T>(Action action) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new InvalidOperationException($"Expected {typeof(T).Name}");
}

Check("Monthly dates include leap February", () => Dates(
    [D(2024, 1), D(2024, 2), D(2024, 3)],
    CollectionPeriodRules.GetDueDates(D(2024, 1), null, 1, D(2024, 1), D(2024, 4))));
Check("Quarterly anchor survives a later query window", () => Dates(
    [D(2026, 4), D(2026, 7), D(2026, 10)],
    CollectionPeriodRules.GetDueDates(D(2020, 1), null, 3, D(2026, 2), D(2027, 1))));
Check("No historical 2016 reset", () => Dates(
    [D(2017, 5)],
    CollectionPeriodRules.GetDueDates(D(2014, 5), null, 36, D(2016, 1), D(2019, 1))));
Check("Middle-of-month query excludes earlier due date", () => Dates(
    [D(2026, 2)],
    CollectionPeriodRules.GetDueDates(D(2026, 1), null, 1, D(2026, 1, 15), D(2026, 3))));
Check("Exclusive tariff end excludes the boundary", () => Dates(
    [D(2026, 1), D(2026, 2)],
    CollectionPeriodRules.GetDueDates(D(2026, 1), D(2026, 3), 1, D(2026, 1), D(2027, 1))));
Check("No dates outside the tariff", () => Dates([], CollectionPeriodRules.GetDueDates(
    D(2026, 5), null, 1, D(2026, 1), D(2026, 4))));
Check("All legacy intervals use their actual cadence", () =>
{
    foreach (var interval in new[] { 1, 2, 3, 4, 6, 12, 24, 36 })
        Equal(72 / interval, CollectionPeriodRules.GetDueDates(D(2020, 1), null, interval, D(2020, 1), D(2026, 1)).Count());
});
Check("Unsupported intervals are rejected", () => Throws<ArgumentOutOfRangeException>(() =>
    CollectionPeriodRules.GetDueDates(D(2026, 1), null, 5, D(2026, 1), D(2027, 1))));
Check("Mid-month start renews on the same day", () => Dates([D(2026, 1, 15), D(2026, 2, 15)],
    CollectionPeriodRules.GetDueDates(D(2026, 1, 15), null, 1, D(2026, 1), D(2026, 3))));
Check("Short month retains original anniversary for later periods", () => Dates([D(2026, 1, 31), D(2026, 2, 28), D(2026, 3, 31)],
    CollectionPeriodRules.GetDueDates(D(2026, 1, 31), null, 1, D(2026, 1), D(2026, 4))));
Check("Leap February anniversary returns in a leap year", () => Dates([D(2025, 2, 28), D(2026, 2, 28), D(2027, 2, 28), D(2028, 2, 29)],
    CollectionPeriodRules.GetDueDates(D(2024, 2, 29), null, 12, D(2025, 1), D(2029, 1))));
Check("Empty query window is rejected", () => Throws<ArgumentException>(() =>
    CollectionPeriodRules.GetDueDates(D(2026, 1), null, 1, D(2026, 1), D(2026, 1))));
Check("Reversed tariff is rejected", () => Throws<ArgumentException>(() =>
    CollectionPeriodRules.GetDueDates(D(2026, 1), D(2025, 1), 1, D(2026, 1), D(2027, 1))));
Check("Inclusive leap-day end converts losslessly", () => Equal<DateOnly?>(D(2024, 3), CollectionPeriodRules.ToExclusiveEnd(D(2024, 2, 29))));
Check("Open end stays open", () => Equal<DateOnly?>(null, CollectionPeriodRules.ToExclusiveEnd(null)));
Check("Maximum date is an exception rather than fabricated infinity", () =>
    Throws<ArgumentOutOfRangeException>(() => CollectionPeriodRules.ToExclusiveEnd(DateOnly.MaxValue)));
Check("Schedule near year 9999 does not overflow", () => Dates([D(9999, 12)],
    CollectionPeriodRules.GetDueDates(D(9999, 12), null, 36, D(9999, 12), DateOnly.MaxValue)));
Check("Touching periods do not overlap", () => Equal(0, CollectionPeriodRules.FindOverlaps(
    [new(1, D(2026, 1), D(2026, 2)), new(2, D(2026, 2), null)]).Count));
Check("Nested periods find the earlier long interval", () =>
{
    var result = CollectionPeriodRules.FindOverlaps(
        [new(3, D(2026, 4), D(2026, 5)), new(1, D(2026, 1), D(2026, 12)), new(2, D(2026, 2), D(2026, 3))]);
    Equal(2, result.Count);
    Equal(new RatePeriodOverlap(1, 3), result[1]);
});
Check("Open period overlaps all subsequent periods", () => Equal(2, CollectionPeriodRules.FindOverlaps(
    [new(1, D(2026, 1), null), new(2, D(2027, 1), D(2027, 2)), new(3, D(2028, 1), null)]).Count));
Check("Duplicate starts conflict", () => Equal(1, CollectionPeriodRules.FindOverlaps(
    [new(1, D(2026, 1), D(2026, 3)), new(2, D(2026, 1), D(2026, 2))]).Count));
Check("Duplicate row identifiers are rejected", () => Throws<ArgumentException>(() =>
    CollectionPeriodRules.FindOverlaps([new(1, D(2026, 1), null), new(1, D(2026, 2), null)])));
Check("Reversed overlap input is rejected", () => Throws<ArgumentException>(() =>
    CollectionPeriodRules.FindOverlaps([new(1, D(2026, 2), D(2026, 1))])));
bool Valid(CollectionFollowUpQuery query) => Validator.TryValidateObject(query, new ValidationContext(query), new List<ValidationResult>(), true);
Check("Default query is valid", () => Equal(true, Valid(new())));
Check("Page size above 100 is rejected", () => Equal(false, Valid(new() { PageSize = 101 })));
Check("Page zero is rejected", () => Equal(false, Valid(new() { Page = 0 })));
Check("Offset overflow is rejected", () => Equal(false, Valid(new() { Page = int.MaxValue, PageSize = 100 })));
Check("Unknown sort cannot enter a query", () => Equal(false, Valid(new() { SortBy = (CollectionFollowUpSort)999 })));
Check("Unknown view is rejected", () => Equal(false, Valid(new() { View = (CollectionFollowUpView)999 })));
Check("Partial period range is rejected", () => Equal(false, Valid(new() { PeriodFrom = D(2026, 1) })));
Check("Reversed period filter is rejected", () => Equal(false, Valid(new() { PeriodFrom = D(2026, 2), PeriodTo = D(2026, 1) })));
Check("Single-month filter is supported", () => Equal(true, Valid(new() { PeriodFrom = D(2026, 2), PeriodTo = D(2026, 2) })));
Check("Mid-month period boundary is rejected", () => Equal(false, Valid(new() { PeriodFrom = D(2026, 1, 15), PeriodTo = D(2026, 2) })));
Check("Reversed payment dates are rejected", () => Equal(false, Valid(new() { PaymentDateFrom = D(2026, 2), PaymentDateTo = D(2026, 1) })));
Check("Negative customer identifier is rejected", () => Equal(false, Valid(new() { CustomerId = -1 })));
Check("Oversized search is rejected", () => Equal(false, Valid(new() { Search = new string('a', 201) })));
Check("No tariff cannot resolve currency", () => Equal(
    new PaymentCurrencyCandidateResult(PaymentCurrencyCandidateStatus.NoRateCandidate, 0, null),
    PaymentCurrencyCandidateRules.Evaluate([])));
Check("Single mapped tariff resolves currency", () => Equal(
    new PaymentCurrencyCandidateResult(PaymentCurrencyCandidateStatus.SingleRateCurrency, 1, 2),
    PaymentCurrencyCandidateRules.Evaluate([new(10, 2)])));
Check("Unknown currency blocks even a known candidate", () => Equal(
    new PaymentCurrencyCandidateResult(PaymentCurrencyCandidateStatus.UnknownCurrencyPresent, 2, null),
    PaymentCurrencyCandidateRules.Evaluate([new(10, 2), new(11, null)])));
Check("Different currencies never choose first row", () => Equal(
    new PaymentCurrencyCandidateResult(PaymentCurrencyCandidateStatus.MultipleCurrencies, 2, null),
    PaymentCurrencyCandidateRules.Evaluate([new(10, 2), new(11, 1)])));
Check("Same currency does not hide conflicting tariffs", () => Equal(
    new PaymentCurrencyCandidateResult(PaymentCurrencyCandidateStatus.MultipleRatesSameCurrency, 2, null),
    PaymentCurrencyCandidateRules.Evaluate([new(10, 2), new(11, 2)])));
Check("Unknown currency takes precedence over multiple currencies", () => Equal(
    PaymentCurrencyCandidateStatus.UnknownCurrencyPresent,
    PaymentCurrencyCandidateRules.Evaluate([new(10, 1), new(11, 2), new(12, null)]).Status));
Check("Currency classification is independent of row order", () => Equal(
    PaymentCurrencyCandidateRules.Evaluate([new(10, 1), new(11, 2)]),
    PaymentCurrencyCandidateRules.Evaluate([new(11, 2), new(10, 1)])));
Check("Duplicate source rate is an input error", () => Throws<ArgumentException>(() =>
    PaymentCurrencyCandidateRules.Evaluate([new(10, 1), new(10, 1)])));
Check("Invalid currency identifier is an input error", () => Throws<ArgumentException>(() =>
    PaymentCurrencyCandidateRules.Evaluate([new(10, 0)])));
Check("Invalid source rate identifier is an input error", () => Throws<ArgumentException>(() =>
    PaymentCurrencyCandidateRules.Evaluate([new(0, 1)])));
Check("Null candidate input is rejected", () => Throws<ArgumentNullException>(() =>
    PaymentCurrencyCandidateRules.Evaluate(null!)));
var rows = new[] { 3, 1, 2 }.Select(id => new CollectionFollowUpReadRow
{
    ContractId = id, RatePeriodId = id, Period = D(2026, 1),
    SubscriberCode = id.ToString(), CustomerName = id.ToString(), ServiceTypeName = id.ToString(),
    ContractAmount = id, PaymentAmount = id, RemainingAmount = id
}).AsQueryable();
Check("All allowed sorts support both directions", () =>
{
    foreach (var sort in Enum.GetValues<CollectionFollowUpSort>())
    {
        Equal(1L, CollectionFollowUpPaging.Apply(rows, new() { SortBy = sort }).First().ContractId);
        Equal(sort == CollectionFollowUpSort.Period ? 1L : 3L,
            CollectionFollowUpPaging.Apply(rows, new() { SortBy = sort, Desc = true }).First().ContractId);
    }
});
Check("Page two uses stable tie breakers", () => Equal(2L,
    CollectionFollowUpPaging.Apply(rows, new() { Page = 2, PageSize = 1 }).Single().ContractId));
Check("Past-last page is empty", () => Equal(0,
    CollectionFollowUpPaging.Apply(rows, new() { Page = 4, PageSize = 1 }).Count()));
Check("Empty source stays empty", () => Equal(0,
    CollectionFollowUpPaging.Apply(Array.Empty<CollectionFollowUpReadRow>().AsQueryable(), new()).Count()));
Check("Invalid sort is rejected at service boundary", () => Throws<ValidationException>(() =>
    CollectionFollowUpPaging.Apply(rows, new() { SortBy = (CollectionFollowUpSort)999 })));
Check("Oversized page is rejected at service boundary", () => Throws<ValidationException>(() =>
    CollectionFollowUpPaging.Apply(rows, new() { PageSize = 101 })));
Check("Offset overflow is rejected before query composition", () => Throws<ValidationException>(() =>
    CollectionFollowUpPaging.Apply(rows, new() { Page = int.MaxValue, PageSize = 100 })));
Check("Null amount is not replaced with zero", () => Equal<decimal?>(null,
    CollectionFollowUpPaging.Apply(new[] { new CollectionFollowUpReadRow() }.AsQueryable(), new()).Single().RemainingAmount));
Check("Paging remains deferred", () =>
{
    var deferred = Enumerable.Range(1, 1).Select<int, CollectionFollowUpReadRow>(_ =>
        throw new InvalidOperationException("Must not enumerate"));
    var expression = CollectionFollowUpPaging.Apply(deferred.AsQueryable(), new()).Expression.ToString();
    Equal(true, expression.Contains("Skip(0).Take(25)"));
});
Check("All legacy payment types preserve verified intervals and codes", () =>
{
    var expected = new[] { (26, "YEARLY", 12), (27, "EVERY_3_MONTHS", 3), (28, "EVERY_2_MONTHS", 2),
        (29, "EVERY_36_MONTHS", 36), (30, "MONTHLY", 1), (31, "EVERY_6_MONTHS", 6),
        (32, "EVERY_4_MONTHS", 4), (33, "EVERY_24_MONTHS", 24) };
    foreach (var (id, code, months) in expected)
        Equal(new PaymentFrequencyMapping(code, (short)months), LegacyPaymentFrequencyRules.Resolve(id));
});
Check("Unknown legacy frequency never defaults to monthly", () =>
{
    foreach (int? id in new int?[] { null, 0, -1, 1, 25, 34, int.MaxValue })
        Equal<PaymentFrequencyMapping?>(null, LegacyPaymentFrequencyRules.Resolve(id));
});
Check("Every mapped frequency is supported by calendar", () =>
{
    foreach (var id in Enumerable.Range(26, 8))
        Equal(true, CollectionPeriodRules.IsSupportedInterval(LegacyPaymentFrequencyRules.Resolve(id)!.IntervalMonths));
});
Check("Mid-period raise keeps renewal day and previous charge", () =>
{
    var result = CollectionAccrualRules.Calculate([
        new(1, D(2026, 1, 15), D(2026, 2, 20), D(2026, 1, 15), 1, 1000m, 1, CollectionBillingBehavior.Billable),
        new(2, D(2026, 2, 20), null, D(2026, 1, 15), 1, 1500m, 1, CollectionBillingBehavior.Billable)
    ], D(2026, 1), D(2026, 4));
    Equal(3, result.Count);
    Equal(new CollectionCharge(1, D(2026, 2, 15), 1000m, 1), result[1]);
    Equal(new CollectionCharge(2, D(2026, 3, 15), 1500m, 1), result[2]);
});
Check("Suspension creates no catch-up debt on reactivation", () =>
{
    var result = CollectionAccrualRules.Calculate([
        new(1, D(2026, 1, 15), D(2026, 2, 1), D(2026, 1, 15), 1, 1000m, 1, CollectionBillingBehavior.Billable),
        new(2, D(2026, 2, 1), D(2026, 4, 10), D(2026, 1, 15), 1, 1000m, 1, CollectionBillingBehavior.Suspended),
        new(3, D(2026, 4, 10), null, D(2026, 4, 10), 1, 1000m, 1, CollectionBillingBehavior.Billable)
    ], D(2026, 1), D(2026, 6));
    Equal(3, result.Count);
    Equal(D(2026, 4, 10), result[1].DueDate);
    Equal(D(2026, 5, 10), result[2].DueDate);
});
Check("Free tariff with nonzero source amount produces no charge", () => Equal(0,
    CollectionAccrualRules.Calculate([new(1, D(2026, 1), null, D(2026, 1), 1, 1000m, 1,
        CollectionBillingBehavior.Free)], D(2026, 1), D(2027, 1)).Count));
Check("Quarterly amount is charged in full without monthly allocation", () =>
{
    var result = CollectionAccrualRules.Calculate([new(1, D(2026, 1, 15), null, D(2026, 1, 15), 3, 3000m, 1,
        CollectionBillingBehavior.Billable)], D(2026, 1), D(2026, 7));
    Equal(2, result.Count);
    Equal(3000m, result[1].Amount);
    Equal(D(2026, 4, 15), result[1].DueDate);
});
Check("Missing currency cannot become zero debt", () => Throws<ArgumentException>(() =>
    CollectionAccrualRules.Calculate([new(1, D(2026, 1), null, D(2026, 1), 1, 1000m, null,
        CollectionBillingBehavior.Billable)], D(2026, 1), D(2027, 1))));
Check("Missing timeline cannot become zero debt", () => Throws<ArgumentException>(() =>
    CollectionAccrualRules.Calculate([], D(2026, 1), D(2027, 1))));
Check("Overlapping tariffs never double bill", () => Throws<ArgumentException>(() =>
    CollectionAccrualRules.Calculate([
        new(1, D(2026, 1), null, D(2026, 1), 1, 1000m, 1, CollectionBillingBehavior.Billable),
        new(2, D(2026, 2), null, D(2026, 1), 1, 1500m, 1, CollectionBillingBehavior.Billable)
    ], D(2026, 1), D(2027, 1))));
Check("Gaps are not silently treated as free periods", () => Throws<ArgumentException>(() =>
    CollectionAccrualRules.Calculate([
        new(1, D(2026, 1), D(2026, 2), D(2026, 1), 1, 1000m, 1, CollectionBillingBehavior.Billable),
        new(2, D(2026, 3), null, D(2026, 1), 1, 1500m, 1, CollectionBillingBehavior.Billable)
    ], D(2026, 1), D(2027, 1))));
Check("Separate POS identities remain separate", () =>
{
    Equal("FINANSBANK_POS", LegacyCollectionDefinitionRules.PaymentMethod(48));
    Equal("ISBANK_POS", LegacyCollectionDefinitionRules.PaymentMethod(56));
});
Check("Free method preserves source meaning without billing conversion", () => Equal("LEGACY_FREE", LegacyCollectionDefinitionRules.PaymentMethod(55)));
Check("Contract status does not become subscription status", () =>
{
    Equal("EXISTS", LegacyCollectionDefinitionRules.ContractStatus(13));
    Equal("NONE", LegacyCollectionDefinitionRules.ContractStatus(15));
    Equal("UNKNOWN", LegacyCollectionDefinitionRules.ContractStatus(14));
});
Check("Unknown definition identities have no fallback", () =>
{
    foreach (int? id in new int?[] { null, -1, 0, 999 })
    {
        Equal<string?>(null, LegacyCollectionDefinitionRules.PaymentMethod(id));
        Equal<string?>(null, LegacyCollectionDefinitionRules.ContractStatus(id));
        Equal<string?>(null, LegacyCollectionDefinitionRules.SubscriptionStatus(id));
        Equal<string?>(null, LegacyCollectionDefinitionRules.GroupStatus(id));
    }
});
Check("All group labels retain distinct identities", () => Equal(7,
    Enumerable.Range(1, 7).Select(id => LegacyCollectionDefinitionRules.GroupStatus(id)).Distinct().Count()));
Check("Same actor and payload can replay a committed receipt", () => Equal(true,
    CollectionPaymentReplayRules.CanReplay(1, 1, new byte[32], new byte[32])));
Check("Different payload cannot reuse a request", () =>
{
    var changed = new byte[32]; changed[31] = 1;
    Equal(false, CollectionPaymentReplayRules.CanReplay(1, 1, new byte[32], changed));
});
Check("Another user cannot replay a receipt", () => Equal(false,
    CollectionPaymentReplayRules.CanReplay(1, 2, new byte[32], new byte[32])));
Check("Missing payload hash is rejected", () => Throws<ArgumentException>(() =>
    CollectionPaymentReplayRules.CanReplay(1, 1, [], new byte[32])));
Console.WriteLine($"{passed} offline collection checks passed. No database access.");
