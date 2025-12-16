using Core.Enums;

namespace Model.Dtos.WorkFlowDtos.YkbDtos.YkbReport
{
    public class YkbReportQueryParams
    {
        // ---- Tarih filtreleri ----
        public DateTimeOffset? CreatedFrom { get; set; }      // WorkFlow.CreatedDate
        public DateTimeOffset? CreatedTo { get; set; }
        public DateTimeOffset? ServicesDateFrom { get; set; } // ServicesRequest.ServicesDate
        public DateTimeOffset? ServicesDateTo { get; set; }

        // ---- Metin arama ----
        public string? Search { get; set; }                   // RequestNo, Title, CustomerCompany vb.

        // ---- Kimlik/kod bazlı ----
        public long? CustomerId { get; set; }
        public string? CustomerName { get; set; }             // contains
        public long? TechnicianId { get; set; }               // WorkFlow.ApproverTechnicianId
        public long? ServiceTypeId { get; set; }
        public string? StepCode { get; set; }                 // WF.CurrentStep.Code
        public string? RequestNo { get; set; }
        public long? ProductId { get; set; }                  // ServicesRequestProduct.ProductId
        public string? ProductCode { get; set; }              // contains (Product.ProductCode)

        // ---- Statüler (çoklu seçim destekli) ----
        public List<WorkFlowStatus>? WorkFlowStatuses { get; set; }
        public List<TechnicalServiceStatus>? TechnicalStatuses { get; set; }
        public List<PricingStatus>? PricingStatuses { get; set; }
        public List<FinalApprovalStatus>? FinalApprovalStatuses { get; set; }

        // ---- Coğrafi/flag ----
        public bool? IsAgreement { get; set; }
        public bool? IsLocationValid { get; set; }
        public bool? HasImages { get; set; }                  // Teknik servis foto/form var mı?

        // ---- Sıralama ----
        // created desc / servicesDate asc / total desc vs.
        public string? SortBy { get; set; } = "created_desc";

        /// <summary>1'den başlar.</summary>
        public int Page { get; set; } = 1;

        /// <summary>Varsayılan 20, üst sınır Normalize ile kısıtlanabilir.</summary>
        public int PageSize { get; set; } = 20;

        /// <summary>Sayfalama değerlerini güvenli aralığa çeker.</summary>
        public void Normalize(int maxPageSize = 200)
        {
            if (Page < 1) Page = 1;
            if (PageSize < 1) PageSize = 1;
            if (PageSize > maxPageSize) PageSize = maxPageSize;
        }
    }


}
