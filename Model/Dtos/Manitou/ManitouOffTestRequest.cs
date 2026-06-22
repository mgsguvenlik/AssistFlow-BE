using System.Text.Json.Serialization;

namespace Model.Dtos.Manitou
{
    public sealed class ManitouOffTestRequest
    {
        [JsonPropertyName("serialNo")]
        public int SerialNo { get; set; }

        [JsonPropertyName("logSequence")]
        public long LogSequence { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("isNew")]
        public bool IsNew { get; set; } = false;

        // Testin başladığı zaman.
        // Örnek format: "2024-06-20 14:00:00"
        [JsonPropertyName("utcFrom")]
        public string UtcFrom { get; set; } = string.Empty;

        // Şu anki bitiş zamanı.
        // Örnek format: "2024-06-20 14:35:00"
        [JsonPropertyName("utcTo")]
        public string UtcTo { get; set; } = string.Empty;
    }
}