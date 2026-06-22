using System.Text.Json.Serialization;

namespace Model.Dtos.Manitou
{
    public sealed class ManitouSystemTestZoneResult
    {
        [JsonPropertyName("serialNo")]
        public int SerialNo { get; set; }

        [JsonPropertyName("systemNo")]
        public int SystemNo { get; set; }

        [JsonPropertyName("areaId")]
        public string? AreaId { get; set; }

        [JsonPropertyName("zoneId")]
        public string? ZoneId { get; set; }

        [JsonPropertyName("activityStatus")]
        public string? ActivityStatus { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("eventCategoryId")]
        public string? EventCategoryId { get; set; }

        [JsonPropertyName("expectedSignalCount")]
        public int ExpectedSignalCount { get; set; }

        [JsonPropertyName("testSignalCount")]
        public int TestSignalCount { get; set; }

        [JsonIgnore]
        public bool IsReceived => TestSignalCount > 0;
    }
}