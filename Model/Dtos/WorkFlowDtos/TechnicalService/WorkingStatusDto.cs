using Model.Dtos.Manitou;

namespace Model.Dtos.WorkFlowDtos.TechnicalService
{
    public sealed class WorkingStatusDto
    {
        public long RequestId { get; set; }
        public string RequestNo { get; set; } = string.Empty;

        public int SerialNo { get; set; }

        public bool IsActive { get; set; }

        public bool IsCompleted { get; set; }

        public DateTimeOffset StartedAtUtc { get; set; }

        public DateTimeOffset PlannedEndAtUtc { get; set; }

        public long RemainingSeconds { get; set; }

        public int ExtendCount { get; set; }

        public List<ManitouSystemTestZoneResult> Zones { get; set; } = new();

        public ManitouCustomerActivityResponse? Activity { get; set; }

        public List<string> ReceivedZones { get; set; } = new();

        public List<string> MissingZones { get; set; } = new();

        public bool AllZonesReceived => MissingZones.Count == 0;
    }
}