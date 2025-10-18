using Core.Enums;
using Model.Concrete.WorkFlows;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.WorkFlowDtos.WorkFlow
{
    public class WorkFlowCreateDto
    {
        public string RequestTitle { get; set; } = null!;

        [Required, MaxLength(100)]
        public string RequestNo { get; set; } = null!;

        public long StatuId { get; set; } // WorkFlowStatus FK
        public WorkFlowPriority Priority { get; set; } = WorkFlowPriority.Normal;

        public bool IsCancelled { get; set; } = false;
        public bool IsComplated { get; set; } = false;
        public bool IsLocationValid { get; set; } = true;

        public WorkFlowReconciliationStatus ReconciliationStatus { get; set; }
            = WorkFlowReconciliationStatus.Pending;
    }
}
