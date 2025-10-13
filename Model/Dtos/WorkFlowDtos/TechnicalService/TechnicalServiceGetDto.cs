using Core.Enums;
using Model.Dtos.WorkFlowDtos.TechnicalServiceImage;
using Model.Dtos.WorkFlowDtos.UsedMaterial;

namespace Model.Dtos.WorkFlowDtos.TechnicalService
{
    public class TechnicalServiceGetDto
    {
        public long Id { get; set; }
        public string RequestNo { get; set; } = string.Empty;

        public long? ServiceTypeId { get; set; }
        public string? ServiceTypeName { get; set; }

        public TimeSpan? StartHour { get; set; }
        public TimeSpan? EndHour { get; set; }

        public DateTimeOffset? StartTime { get; set; }
        public DateTimeOffset? EndTime { get; set; }

        public string? ProblemDescription { get; set; }
        public string? ResolutionAndActions { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        public TechnicalServiceStatus ServicesStatus { get; set; }
        public ServicesCostStatus ServicesCostStatus { get; set; }

        public List<TechnicalServiceImageGetDto> ServicesImages { get; set; } = new();
        public List<TechnicalServiceFormImageGetDto> ServiceRequestFormImages { get; set; } = new();
        public List<UsedMaterialGetDto> UsedMaterials { get; set; } = new();
    }
}
