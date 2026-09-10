namespace Business.Services.Crm.Collections.Calculation;

/// <summary>
/// Classifies already selected contract/period rate candidates using approved target currency mappings.
/// Does not select rates, infer currency from names, or authorize a payment import.
/// </summary>
public static class PaymentCurrencyCandidateRules
{
    public static PaymentCurrencyCandidateResult Evaluate(IEnumerable<PaymentCurrencyCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        var identifiers = new HashSet<long>();
        var currencies = new HashSet<int>();
        var unknown = false;
        foreach (var candidate in candidates)
        {
            if (candidate.SourceRateId <= 0 || !identifiers.Add(candidate.SourceRateId))
                throw new ArgumentException("Adayların kaynak tarife kimlikleri farklı ve pozitif olmalıdır.", nameof(candidates));
            if (candidate.TargetCurrencyTypeId is <= 0)
                throw new ArgumentException("Eşlenen para birimi kimlikleri pozitif olmalıdır.", nameof(candidates));
            if (candidate.TargetCurrencyTypeId is int currencyId)
                currencies.Add(currencyId);
            else
                unknown = true;
        }

        var status = identifiers.Count == 0 ? PaymentCurrencyCandidateStatus.NoRateCandidate
            : unknown ? PaymentCurrencyCandidateStatus.UnknownCurrencyPresent
            : currencies.Count > 1 ? PaymentCurrencyCandidateStatus.MultipleCurrencies
            : identifiers.Count > 1 ? PaymentCurrencyCandidateStatus.MultipleRatesSameCurrency
            : PaymentCurrencyCandidateStatus.SingleRateCurrency;

        return new(status, identifiers.Count,
            status == PaymentCurrencyCandidateStatus.SingleRateCurrency ? currencies.Single() : null);
    }
}

public enum PaymentCurrencyCandidateStatus
{
    NoRateCandidate,
    UnknownCurrencyPresent,
    MultipleCurrencies,
    MultipleRatesSameCurrency,
    SingleRateCurrency
}

public sealed record PaymentCurrencyCandidate(long SourceRateId, int? TargetCurrencyTypeId);
public sealed record PaymentCurrencyCandidateResult(
    PaymentCurrencyCandidateStatus Status, int CandidateCount, int? ResolvedCurrencyTypeId);
