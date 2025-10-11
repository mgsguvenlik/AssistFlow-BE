using Model.Concrete.WorkFlows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.WorkFlowDtos.WorkFlow
{
    public class WorkFlowUpdateDto
    {
        public long Id { get; set; }
        public string? RequestTitle { get; set; }
        public string? RequestNo { get; set; }
        public long? StatuId { get; set; }
        public WorkFlowPriority? Priority { get; set; }
        public bool? IsCancelled { get; set; }
        public bool? IsComplated { get; set; }
        public WorkFlowReconciliationStatus? ReconciliationStatus { get; set; }
    }
}
