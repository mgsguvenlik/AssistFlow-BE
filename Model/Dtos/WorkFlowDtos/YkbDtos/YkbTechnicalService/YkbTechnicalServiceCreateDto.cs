using Core.Enums;

namespace Model.Dtos.WorkFlowDtos.YkbDtos.YkbTechnicalService
{
    public class YkbTechnicalServiceCreateDto
    {
        public string RequestNo { get; set; } = string.Empty;
        public long? ServiceTypeId { get; set; }

        public DateTimeOffset? StartTime { get; set; }
        public DateTimeOffset? EndTime { get; set; }

        public string? ProblemDescription { get; set; }
        public string? ResolutionAndActions { get; set; }

        public string? Latitude { get; set; }
        public string? Longitude { get; set; }
        public string? StartLocation { get; set; }
        public string? EndLocation { get; set; }
        public bool IsLocationCheckRequired { get; set; } = true;

        public TechnicalServiceStatus ServicesStatus { get; set; }
        public ServicesCostStatus ServicesCostStatus { get; set; }
    }
}
