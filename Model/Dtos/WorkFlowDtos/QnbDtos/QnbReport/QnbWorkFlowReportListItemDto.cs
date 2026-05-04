using Core.Enums;

namespace Model.Dtos.WorkFlowDtos.QnbDtos.QnbReport
{
    public class QnbWorkFlowReportListItemDto
    {
        public string RequestNo { get; set; } = string.Empty;

        public string Title { get; set; } = "Servis Talebi";
        public WorkFlowStatus WorkFlowStatus { get; set; }
        public string? StepCode { get; set; }
        public DateTimeOffset CreatedDate { get; set; }

        public long CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }

        public DateTimeOffset ServicesDate { get; set; }
        public long ServiceTypeId { get; set; }
        public string? ServiceTypeName { get; set; }

        public long? TechnicianId { get; set; }
        public string? TechnicianName { get; set; }

        public string Currency { get; set; } = "TRY";
        public decimal? Subtotal { get; set; }

        public TechnicalServiceStatus? TechnicalStatus { get; set; }
        public PricingStatus? PricingStatus { get; set; }
        public FinalApprovalStatus? FinalApprovalStatus { get; set; }

        public bool HasImages { get; set; }
    }
}