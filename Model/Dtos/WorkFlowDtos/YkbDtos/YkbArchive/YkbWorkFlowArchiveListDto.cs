using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.WorkFlowDtos.YkbDtos.YkbArchive
{
    public class YkbWorkFlowArchiveListDto
    {
        public long Id { get; set; }
        public string RequestNo { get; set; } = default!;
        public string? CustomerName { get; set; }
        public string? TechnicianName { get; set; }
        public string? WorkFlowStatus { get; set; }
        public string ArchiveReason { get; set; } = default!;
        public DateTime ArchivedAt { get; set; }
    }
}
