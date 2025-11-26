using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.WorkFlowDtos.WorkFlowArchive
{
    public class WorkFlowArchiveListDto
    {
        public long Id { get; set; }
        public string RequestNo { get; set; } = default!;
        public string? CustomerName { get; set; }
        public string? TechnicianName { get; set; }
        public string? WorkFlowStatus { get; set; }
        public string ArchiveReason { get; set; } = default!;
        public DateTime ArchivedAt { get; set; }
    }
    //public class PagedResult<T>
    //{
    //    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    //    public int Page { get; set; }
    //    public int PageSize { get; set; }
    //    public int TotalCount { get; set; }
    //    public int TotalPages { get; set; }
    //}

}
