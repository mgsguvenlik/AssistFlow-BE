namespace Business.Services.Crm.Collections.Calculation;

/// <summary>Source identities map to stable codes, never to fabricated target database identities.</summary>
public static class LegacyCollectionDefinitionRules
{
    public static string? PaymentMethod(int? sourceId) => sourceId switch
    {
        48 => "FINANSBANK_POS", 49 => "GTS", 50 => "IVR", 51 => "WEB", 52 => "MAIL_ORDER",
        53 => "OFFSET", 54 => "CASH_MANUAL", 55 => "LEGACY_FREE", 56 => "ISBANK_POS", 57 => "BANK_TRANSFER",
        _ => null
    };

    public static string? SubscriptionStatus(int? sourceId) => sourceId switch
    {
        12 => "ACTIVE", 13 => "FROZEN", _ => null
    };

    public static string? ContractStatus(int? sourceId) => sourceId switch
    {
        13 => "EXISTS", 14 => "UNKNOWN", 15 => "NONE", _ => null
    };

    public static string? GroupStatus(int? sourceId) => sourceId switch
    {
        1 => "OFFSET", 2 => "CANCELLED", 3 => "PENDING", 4 => "FREE", 5 => "INVOICED",
        6 => "PAID", 7 => "APPROVED", _ => null
    };
}
