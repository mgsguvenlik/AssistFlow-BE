using Core.Enums;

namespace Model.Dtos.WorkFlowDtos.YkbDtos.YkbWorkFlow
{
        public class YkbWorkFlowQueryParams
        {
            public int Page { get; set; } = 1;
            public int PageSize { get; set; } = 20;
            public string? Search { get; set; }
            public string? Sort { get; set; }
            public bool Desc { get; set; } = false;

            public long? CurrentStepId { get; set; }
            public string? StepCode { get; set; }

            public WorkFlowPriority? Priority { get; set; }
            public List<WorkFlowPriority>? Priorities { get; set; }

            public DateTimeOffset? StartDate { get; set; }
            public DateTimeOffset? EndDate { get; set; }

            // Yeni filtreler
            public ServicesCostStatus? ServicesCostStatus { get; set; }
            public List<ServicesCostStatus>? ServicesCostStatuses { get; set; }

            public long? ServiceTypeId { get; set; }
            public List<long>? ServiceTypeIds { get; set; }

            public string? City { get; set; }
            public List<string>? Cities { get; set; }

            public long? ProgressApproverId { get; set; }
            public string? ProgressApproverSearch { get; set; }

            public long? CustomerGroupId { get; set; }

            public void Normalize(int maxPageSize = 200)
            {
                if (Page < 1) Page = 1;
                if (PageSize < 1) PageSize = 1;
                if (PageSize > maxPageSize) PageSize = maxPageSize;

                Search = string.IsNullOrWhiteSpace(Search) ? null : Search.Trim();
                Sort = string.IsNullOrWhiteSpace(Sort) ? null : Sort.Trim();
                StepCode = string.IsNullOrWhiteSpace(StepCode) ? null : StepCode.Trim();
                City = string.IsNullOrWhiteSpace(City) ? null : City.Trim();
                ProgressApproverSearch = string.IsNullOrWhiteSpace(ProgressApproverSearch)
                    ? null
                    : ProgressApproverSearch.Trim();

                Priorities = Priorities?
                    .Distinct()
                    .ToList();

                ServicesCostStatuses = ServicesCostStatuses?
                    .Distinct()
                    .ToList();

                ServiceTypeIds = ServiceTypeIds?
                    .Where(x => x > 0)
                    .Distinct()
                    .ToList();

                Cities = Cities?
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }
    }