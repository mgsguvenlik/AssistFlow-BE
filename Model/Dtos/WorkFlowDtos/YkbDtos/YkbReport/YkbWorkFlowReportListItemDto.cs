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
        public List<global::Model.Dtos.ServiceType.ServiceTypeGetDto> ServiceTypes { get; set; } = new();
        public List<long> ServiceTypeIds => ServiceTypes.Select(x => x.Id).ToList();
        private string? serviceTypeName;
        public string? ServiceTypeName
        {
            get => ServiceTypes.Count > 0 ? string.Join(", ", ServiceTypes.Select(x => x.Name)) : serviceTypeName;
            set => serviceTypeName = value;
        }

        // Teknisyen
        public long? TechnicianId { get; set; }
        public string? Name { get; set; }

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
