using Core.Enums;
using Model.Dtos.Customer;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbReviewLog;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbServicesRequestProduct;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbTechnicalServiceImage;
using Model.Dtos.WorkOrderType;

namespace Model.Dtos.WorkFlowDtos.QnbDtos.QnbTechnicalService
{
    public class QnbTechnicalServiceGetDto
    {
        public long Id { get; set; }
        public string RequestNo { get; set; } = string.Empty;

        public string ServiceTitle { get; set; } = string.Empty;
        public string ServiceDescription { get; set; } = string.Empty;
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

        public List<QnbTechnicalServiceImageGetDto> ServicesImages { get; set; } = new();
        public List<QnbTechnicalServiceFormImageGetDto> ServiceRequestFormImages { get; set; } = new();
        public List<QnbServicesRequestProductGetDto> Products { get; set; } = new();

        public List<QnbWorkFlowReviewLogDto> ReviewLogs { get; set; } = new();
        public CustomerGetDto? Customer { get; set; }

        public List<long>? WorkOrderTypeIds { get; set; }
        public List<WorkOrderTypeGetDto> WorkOrderTypes { get; set; } = new();
    }
}