using Core.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.WorkFlowDtos.Report
{
    public class WorkFlowBasicReportQueryParams
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;

        public string? Search { get; set; }
        public string? RequestNo { get; set; }

        public long? CurrentStepId { get; set; }
        public string? StepCode { get; set; }

        public long? ApproverTechnicianId { get; set; }
        public long? CreatedUserId { get; set; }

        public long? CustomerId { get; set; }
        public long? ServiceTypeId { get; set; }

        public WorkFlowPriority? Priority { get; set; }
        public List<WorkFlowPriority>? Priorities { get; set; }

        public WorkFlowStatus? WorkFlowStatus { get; set; }
        public List<WorkFlowStatus>? WorkFlowStatuses { get; set; }

        public ServicesCostStatus? ServicesCostStatus { get; set; }
        public TechnicalServiceStatus? TechnicalServiceStatus { get; set; }

        public bool? IsAgreement { get; set; }
        public bool? IsLocationValid { get; set; }
        public bool? IsProductRequirement { get; set; }

        public DateTimeOffset? CreatedFrom { get; set; }
        public DateTimeOffset? CreatedTo { get; set; }

        public DateTimeOffset? ServicesDateFrom { get; set; }
        public DateTimeOffset? ServicesDateTo { get; set; }

        public DateTimeOffset? TechnicalStartFrom { get; set; }
        public DateTimeOffset? TechnicalStartTo { get; set; }

        public DateTimeOffset? TechnicalEndFrom { get; set; }
        public DateTimeOffset? TechnicalEndTo { get; set; }

        public string? SortBy { get; set; } = "createdDate";
        public bool SortDesc { get; set; } = true;

        public void Normalize(int maxPageSize = 200)
        {
            Page = Page <= 0 ? 1 : Page;
            PageSize = PageSize <= 0 ? 20 : PageSize;
            PageSize = Math.Min(PageSize, maxPageSize);

            Search = string.IsNullOrWhiteSpace(Search) ? null : Search.Trim();
            RequestNo = string.IsNullOrWhiteSpace(RequestNo) ? null : RequestNo.Trim();
            StepCode = string.IsNullOrWhiteSpace(StepCode) ? null : StepCode.Trim().ToUpperInvariant();
            SortBy = string.IsNullOrWhiteSpace(SortBy) ? "createdDate" : SortBy.Trim();
        }
    }
}
