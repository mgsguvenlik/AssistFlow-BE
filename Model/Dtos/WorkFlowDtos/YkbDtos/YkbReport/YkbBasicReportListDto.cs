using Core.Enums;
using Core.Enums.Ykb;
using Model.Dtos.WorkFlowDtos.Report;

namespace Model.Dtos.WorkFlowDtos.YkbDtos.YkbReport
{
    public class YkbBasicReportListDto
    {
        public long WorkFlowId { get; set; }

        public string RequestNo { get; set; } = string.Empty;
        public string RequestTitle { get; set; } = string.Empty;
        public string? YkbServiceTrackNo { get; set; }

        public long? CurrentStepId { get; set; }
        public string? CurrentStepCode { get; set; }
        public string? CurrentStepName { get; set; }

        public string CurrentStepDisplayName => CurrentStepCode switch
        {
            "CF" => "Müşteri Formu Oluşturma",
            "SR" => "Servis Talebi Oluşturma",
            "WH" => "Depo Sevkiyatı",
            "TS" => "Teknik Servis İşlemleri",
            "PRC" => "Fiyatlandırma",
            "APR" => "Onaylama",
            "CAPR" => "Müşteri Onayında",
            "CMP" => "Tamamlandı",
            "CNC" => "İptal Edildi",
            _ => CurrentStepName ?? CurrentStepCode ?? "-"
        };

        public WorkFlowPriority Priority { get; set; }
        public string PriorityName => Priority switch
        {
            WorkFlowPriority.Low => "Düşük",
            WorkFlowPriority.Normal => "Normal",
            WorkFlowPriority.High => "Yüksek",
            WorkFlowPriority.Urgent => "Acil",

            WorkFlowPriority.Region1Normal => "1. Bölge Normal",
            WorkFlowPriority.Region1Urgent => "1. Bölge Acil",

            WorkFlowPriority.Region2Urgent => "2. Bölge Acil",
            WorkFlowPriority.Region2Normal => "2. Bölge Normal",

            WorkFlowPriority.Region3Urgent => "3. Bölge Acil",
            WorkFlowPriority.Region3Normal => "3. Bölge Normal",

            _ => Priority.ToString()
        };

        public WorkFlowStatus WorkFlowStatus { get; set; }
        public string WorkFlowStatusName => WorkFlowStatus switch
        {
            WorkFlowStatus.Pending => "Beklemede",
            WorkFlowStatus.Complated => "Tamamlandı",
            WorkFlowStatus.Cancelled => "İptal Edildi",
            _ => WorkFlowStatus.ToString()
        };

        public DateTimeOffset CreatedDate { get; set; }
        public DateTimeOffset? UpdatedDate { get; set; }

        public long CreatedUserId { get; set; }
        public string? CreatedUserName { get; set; }

        public long? ApproverTechnicianId { get; set; }
        public string? ApproverTechnicianName { get; set; }
        public string? ApproverTechnicianEmail { get; set; }
        public string? TechnicianCity { get; set; }
        public string? TechnicianDistrict { get; set; }

        public long? CustomerId { get; set; }
        public string? CustomerCode { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerCity { get; set; }
        public string? CustomerDistrict { get; set; }

        public long? ServiceTypeId { get; set; }
        public string? ServiceTypeName { get; set; }

        public YkbCustomerFormStatus? CustomerFormStatus { get; set; }
        public DateTime? CustomerFormServicesDate { get; set; }
        public DateTime? CustomerFormPlannedCompletionDate { get; set; }

        public DateTimeOffset? ServicesDate { get; set; }
        public DateTimeOffset? PlannedCompletionDate { get; set; }

        public bool? IsAgreement { get; set; }
        public bool IsLocationValid { get; set; }
        public bool? IsProductRequirement { get; set; }

        public ServicesCostStatus? ServicesCostStatus { get; set; }
        public ServicesRequestStatus? ServicesRequestStatus { get; set; }

        public WarehouseStatus? WarehouseStatus { get; set; }
        public DateTimeOffset? WarehouseDeliveryDate { get; set; }

        public TechnicalServiceStatus? TechnicalServiceStatus { get; set; }
        public DateTimeOffset? TechnicalStartTime { get; set; }
        public DateTimeOffset? TechnicalEndTime { get; set; }
        public double? TechnicalServiceDurationMinutes { get; set; }

        public PricingStatus? PricingStatus { get; set; }
        public decimal? PricingTotalAmount { get; set; }
        public string? Currency { get; set; }

        public FinalApprovalStatus? FinalApprovalStatus { get; set; }
        public decimal? DiscountPercent { get; set; }

        public string? FinalApprovalNotes { get; set; }
        public string? CustomerNote { get; set; }
        public long? CustomerApprovedBy { get; set; }
        public string? CustomerApprovedByName { get; set; }
        public DateTime? CustomerApprovedAt { get; set; }

        public List<WorkOrderTypeLiteDto> WorkOrderTypes { get; set; } = new();

        public DateTimeOffset LastActivityDate => UpdatedDate ?? CreatedDate;
    }
}
