using Core.Enums;
using Model.Dtos.Customer;
using Model.Dtos.WorkFlowDtos.TechnicalServiceImage;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbReviewLog;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbServicesRequestProduct;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbTechnicalServiceImage;

namespace Model.Dtos.WorkFlowDtos.YkbDtos.YkbTechnicalService
{
    public class YkbTechnicalServiceGetDto
    {
        public long Id { get; set; }
        public string RequestNo { get; set; } = string.Empty;

        public long? ServiceTypeId { get; set; }
        public string? ServiceTypeName { get; set; }

        public DateTimeOffset? StartTime { get; set; }
        public DateTimeOffset? EndTime { get; set; }

        public string? ProblemDescription { get; set; }
        public string? ResolutionAndActions { get; set; }

        public string? Latitude { get; set; }
        public string? Longitude { get; set; }
        public string? StartLocation { get; set; }
        public string? EndLocation { get; set; }
        public bool IsLocationCheckRequired { get; set; }
        public TechnicalServiceStatus ServicesStatus { get; set; }
        public ServicesCostStatus ServicesCostStatus { get; set; }

        public List<YkbTechnicalServiceImageGetDto> ServicesImages { get; set; } = new();
        public List<TechnicalServiceFormImageGetDto> ServiceRequestFormImages { get; set; } = new();
        public List<YkbServicesRequestProductGetDto> Products { get; set; } = new();

        public List<YkbWorkFlowReviewLogDto> ReviewLogs { get; set; } = new();
        public CustomerGetDto? Customer { get; set; }
    }
}
