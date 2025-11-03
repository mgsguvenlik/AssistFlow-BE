using Core.Enums;
using Model.Dtos.WorkFlowDtos.ServicesRequestProduct;
using Model.Dtos.WorkFlowDtos.TechnicalServiceImage;
using Model.Dtos.WorkFlowDtos.WorkFlowReviewLog;


namespace Model.Dtos.WorkFlowDtos.TechnicalService
{
    public class TechnicalServiceGetDto
    {
        public long Id { get; set; }
        public string RequestNo { get; set; } = string.Empty;

        public long? ServiceTypeId { get; set; }
        public string? ServiceTypeName { get; set; }

        public DateTimeOffset? StartTime { get; set; }
        public DateTimeOffset? EndTime { get; set; }

        public string? ProblemDescription { get; set; }
        public string? ResolutionAndActions { get; set; }
        public bool IsLocationCheckRequired { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? StartLocation { get; set; }//Örn: "41.01224, 28.976018"
        public string? EndLocation { get; set; }//Örn: "41.01224, 28.976018"

        public TechnicalServiceStatus ServicesStatus { get; set; }
        public ServicesCostStatus ServicesCostStatus { get; set; }
        public List<TechnicalServiceImageGetDto> ServicesImages { get; set; } = new();
        public List<TechnicalServiceFormImageGetDto> ServiceRequestFormImages { get; set; } = new();
        public List<ServicesRequestProductGetDto> Products { get; set; } = new();

        public List<WorkFlowReviewLogDto> ReviewLogs { get; set; } = new();

    }
}
