namespace Business.Services.Crm.Collections.Calculation;

/// <summary>
/// New-contract start policy agreed on 11 September 2026.
/// Not a legacy reconstruction or a reactivation policy; never changes existing financial records.
/// </summary>
public static class CollectionStartRules
{
    public static bool IsIncluded(string? contractStatusCode) => contractStatusCode == "EXISTS";

    public static DateOnly FirstDueDate(DateOnly signatureDate)
    {
        if (signatureDate.Day <= 15) return signatureDate;
        if (signatureDate.Year == 9999 && signatureDate.Month == 12)
            throw new ArgumentOutOfRangeException(nameof(signatureDate), "İlk borç tarihi desteklenen tarih aralığını aşıyor.");
        return signatureDate.AddMonths(1);
    }

    /// <summary>Calendar only: caller still validates tariff, free/frozen state and effective history.</summary>
    public static IEnumerable<DateOnly> GetInitialDueDates(DateOnly signatureDate, int intervalMonths,
        DateOnly? endExclusive, DateOnly windowFrom, DateOnly windowToExclusive)
    {
        var first = FirstDueDate(signatureDate);
        if (!CollectionPeriodRules.IsSupportedInterval(intervalMonths))
            throw new ArgumentOutOfRangeException(nameof(intervalMonths), "Ödeme sıklığı geçersiz.");
        if (windowToExclusive <= windowFrom)
            throw new ArgumentException("Geçerli bir sorgu tarih aralığı gereklidir.");
        if (endExclusive <= signatureDate)
            throw new ArgumentException("Bitiş tarihi başlangıç tarihinden sonra olmalıdır.");
        if (endExclusive <= first) return Array.Empty<DateOnly>();
        return CollectionPeriodRules.GetDueDates(first, endExclusive, intervalMonths,
            windowFrom, windowToExclusive, signatureDate.Day);
    }
}
