using System.Text.Json.Serialization;

namespace Model.Dtos.Manitou
{
    public sealed class ManitouOnTestRequest
    {
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("serialNo")]
        public int SerialNo { get; set; }

        // Örnek format: "2024-06-20 14:00:00"
        [JsonPropertyName("utcFrom")]
        public string UtcFrom { get; set; } = string.Empty;

        // Örnek format: "2024-06-20 15:00:00"
        [JsonPropertyName("utcTo")]
        public string UtcTo { get; set; } = string.Empty;

        [JsonPropertyName("isNew")]
        public bool IsNew { get; set; } = true;
    }
}