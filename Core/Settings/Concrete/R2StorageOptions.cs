using System.ComponentModel.DataAnnotations;

namespace Core.Settings.Concrete;

public sealed class R2StorageOptions
{
    public const string SectionName = "R2Storage";

    [Required]
    public string Endpoint { get; set; } = string.Empty;

    [Required]
    public string BucketName { get; set; } = string.Empty;

    [Required]
    public string AccessKeyId { get; set; } = string.Empty;

    [Required]
    public string SecretAccessKey { get; set; } = string.Empty;

    [Required]
    public string PublicBaseUrl { get; set; } = string.Empty;

    public string KeyPrefix { get; set; } = "uploads";

    // Geçiş sürecinde eski fiziksel dosyaların okunabilmesi için.
    public bool LegacyLocalFallbackEnabled { get; set; } = true;

    public string LegacyLocalRoot { get; set; } = "UploadsStorage";
}