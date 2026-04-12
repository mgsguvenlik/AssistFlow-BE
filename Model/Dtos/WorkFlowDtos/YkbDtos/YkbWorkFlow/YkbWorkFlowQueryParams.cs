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

        // Yeni filtreleme özellikleri
        public long? CurrentStepId { get; set; }
        public string? StepCode { get; set; }
        public WorkFlowPriority? Priority { get; set; }
        public List<WorkFlowPriority>? Priorities { get; set; }

        // Tarih filtreleri
        public DateTimeOffset? StartDate { get; set; }
        public DateTimeOffset? EndDate { get; set; }
        public void Normalize(int maxPageSize = 200)
        {
            if (Page < 1) Page = 1;
            if (PageSize < 1) PageSize = 1;
            if (PageSize > maxPageSize) PageSize = maxPageSize;
        }
    }
}