using System.Text.Json.Serialization;

namespace Model.Dtos.Manitou
{
    public sealed class ManitouOutOfServiceResult
    {
        [JsonPropertyName("serialNo")]
        public int SerialNo { get; set; }

        [JsonPropertyName("logSequence")]
        public long LogSequence { get; set; }

        [JsonPropertyName("sequence")]
        public long Sequence { get; set; }

        [JsonPropertyName("address")]
        public string? Address { get; set; }

        [JsonPropertyName("advancedOnTest")]
        public bool AdvancedOnTest { get; set; }

        [JsonPropertyName("areaId")]
        public string? AreaId { get; set; }

        [JsonPropertyName("customerId")]
        public string? CustomerId { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("eventCategory")]
        public string? EventCategory { get; set; }

        [JsonPropertyName("from")]
        public DateTime? From { get; set; }

        [JsonPropertyName("groupId")]
        public long? GroupId { get; set; }

        [JsonPropertyName("includeSubAccounts")]
        public bool IncludeSubAccounts { get; set; }

        [JsonPropertyName("isNew")]
        public bool IsNew { get; set; }

        [JsonPropertyName("keepSignals")]
        public bool KeepSignals { get; set; }

        [JsonPropertyName("mainSerialNo")]
        public int? MainSerialNo { get; set; }

        [JsonPropertyName("masterSerialNo")]
        public int? MasterSerialNo { get; set; }

        [JsonPropertyName("monitoringGroupId")]
        public long? MonitoringGroupId { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("permanent")]
        public bool Permanent { get; set; }

        [JsonPropertyName("receiverLinePrefix")]
        public string? ReceiverLinePrefix { get; set; }

        [JsonPropertyName("standardCode")]
        public string? StandardCode { get; set; }

        [JsonPropertyName("systemNo")]
        public string? SystemNo { get; set; }

        [JsonPropertyName("technicianSerialNo")]
        public int? TechnicianSerialNo { get; set; }

        [JsonPropertyName("temporary")]
        public bool Temporary { get; set; }

        [JsonPropertyName("to")]
        public DateTime? To { get; set; }

        [JsonPropertyName("transmitterId")]
        public string? TransmitterId { get; set; }

        [JsonPropertyName("transmitterNo")]
        public string? TransmitterNo { get; set; }

        [JsonPropertyName("type")]
        public int? Type { get; set; }

        [JsonPropertyName("utcFrom")]
        public DateTime? UtcFrom { get; set; }

        [JsonPropertyName("utcTo")]
        public DateTime? UtcTo { get; set; }

        [JsonPropertyName("zoneId")]
        public string? ZoneId { get; set; }
    }
}