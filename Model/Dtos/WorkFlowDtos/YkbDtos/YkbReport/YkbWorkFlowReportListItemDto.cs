using Core.Enums;

namespace Model.Dtos.WorkFlowDtos.YkbDtos.YkbReport
{
    public class YkbWorkFlowReportListItemDto
    {
        public string RequestNo { get; set; } = string.Empty;

        // Başlık / Akış
        public string Title { get; set; } = "Servis Talebi";
        public WorkFlowStatus WorkFlowStatus { get; set; }
        public string? StepCode { get; set; }
        public DateTimeOffset CreatedDate { get; set; }

        // Müşteri
        public long CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }

        // Servis
        public DateTimeOffset ServicesDate { get; set; }
        public long ServiceTypeId { get; set; }
        public string? ServiceTypeName { get; set; }

        // Teknisyen
        public long? TechnicianId { get; set; }
        public string? TechnicianName { get; set; }

        // Fiyat/Toplam (Captured-first)
        public string Currency { get; set; } = "TRY";
        public decimal? Subtotal { get; set; }

        // Diğer statüler
        public TechnicalServiceStatus? TechnicalStatus { get; set; }
        public PricingStatus? PricingStatus { get; set; }
        public FinalApprovalStatus? FinalApprovalStatus { get; set; }

        // Görsel flag
        public bool HasImages { get; set; }
    }
}
