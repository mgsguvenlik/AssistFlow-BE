using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.WorkFlowDtos.WorkFlowArchive
{
    public class WorkFlowArchiveDetailDto
    {
        public long Id { get; set; }
        public string RequestNo { get; set; } = default!;
        public DateTime ArchivedAt { get; set; }
        public string ArchiveReason { get; set; } = default!;

        public WorkFlowArchiveSnapshotDto Snapshot { get; set; } = default!;
    }

}
