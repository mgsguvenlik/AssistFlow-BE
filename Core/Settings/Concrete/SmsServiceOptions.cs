namespace Core.Settings.Concrete;

/// <summary>Shared provider settings. Non-production environments always simulate.</summary>
public sealed class SmsServiceOptions
{
    public const string SectionName = "AppSettings";
    // Enable only after the collection SMS migration and template seed are deployed.
    public bool CollectionSmsEnabled { get; set; }
    public bool SmsSimulationEnabled { get; set; } = true;
    public string TelsamSmsEndpoint { get; set; } = "https://sms.telsam.com.tr:9588/sms/create";
    public string TelsamSmsAuthorizationToken { get; set; } = "";
    public string TelsamSmsSender { get; set; } = "MGS";
    public string TelsamSmsTitle { get; set; } = "MGS SMS Bildirimi";
    public int TelsamSmsValidity { get; set; } = 60;
}
