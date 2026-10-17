namespace Business.Services.Crm.Collections.Calculation;

/// <summary>
/// Verified legacy Definition.PaymentType identities, not target database identities.
/// Unknown values must become migration issues rather than defaulting to monthly.
/// Does not decide whether a frequency is available for new contracts.
/// </summary>
public static class LegacyPaymentFrequencyRules
{
    public static PaymentFrequencyMapping? Resolve(int? legacyPaymentTypeId) => legacyPaymentTypeId switch
    {
        26 => new("YEARLY", 12),
        27 => new("EVERY_3_MONTHS", 3),
        28 => new("EVERY_2_MONTHS", 2),
        29 => new("EVERY_36_MONTHS", 36),
        30 => new("MONTHLY", 1),
        31 => new("EVERY_6_MONTHS", 6),
        32 => new("EVERY_4_MONTHS", 4),
        33 => new("EVERY_24_MONTHS", 24),
        _ => null
    };
}

public sealed record PaymentFrequencyMapping(string Code, short IntervalMonths);
