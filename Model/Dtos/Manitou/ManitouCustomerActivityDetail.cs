using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Model.Dtos.Manitou
{
    public sealed class ManitouCustomerActivityDetail
    {
        [JsonPropertyName("serialNo")]
        public int SerialNo { get; set; }

        [JsonPropertyName("sequence")]
        public long Sequence { get; set; }

        [JsonPropertyName("eventNo")]
        public long EventNo { get; set; }

        [JsonPropertyName("activityType")]
        public int ActivityType { get; set; }

        [JsonPropertyName("area")]
        public string? Area { get; set; }

        [JsonPropertyName("binaryObjectType")]
        public string? BinaryObjectType { get; set; }

        [JsonPropertyName("dateTime")]
        public DateTime? DateTime { get; set; }

        [JsonPropertyName("eventCodeId")]
        public string? EventCodeId { get; set; }

        [JsonPropertyName("eventName")]
        public string? EventName { get; set; }

        [JsonPropertyName("owsName")]
        public string? OwsName { get; set; }

        [JsonPropertyName("pointId")]
        public string? PointId { get; set; }

        [JsonPropertyName("qualifier")]
        public int? Qualifier { get; set; }

        [JsonPropertyName("qualifier10")]
        public string? Qualifier10 { get; set; }

        [JsonPropertyName("qualifier2")]
        public int? Qualifier2 { get; set; }

        [JsonPropertyName("qualifier3")]
        public int? Qualifier3 { get; set; }

        [JsonPropertyName("qualifier4")]
        public string? Qualifier4 { get; set; }

        [JsonPropertyName("qualifier5")]
        public string? Qualifier5 { get; set; }

        [JsonPropertyName("qualifier6")]
        public string? Qualifier6 { get; set; }

        [JsonPropertyName("qualifier7")]
        public int? Qualifier7 { get; set; }

        [JsonPropertyName("qualifier8")]
        public int? Qualifier8 { get; set; }

        [JsonPropertyName("qualifier9")]
        public int? Qualifier9 { get; set; }

        [JsonPropertyName("rowType")]
        public string? RowType { get; set; }

        [JsonPropertyName("showDate")]
        public bool ShowDate { get; set; }

        [JsonPropertyName("showTime")]
        public bool ShowTime { get; set; }

        [JsonPropertyName("signalFlags")]
        public int SignalFlags { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("userId")]
        public string? UserId { get; set; }

        [JsonPropertyName("utcTime")]
        public DateTime? UtcTime { get; set; }

        [JsonPropertyName("workstationNumber")]
        public int WorkstationNumber { get; set; }

        [JsonPropertyName("zone")]
        public string? Zone { get; set; }
    }
}
