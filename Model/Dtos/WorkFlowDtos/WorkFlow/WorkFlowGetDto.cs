using Model.Abstractions;
using Model.Concrete.WorkFlows;
using Model.Dtos.WorkFlowDtos.WorkFlowStatus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.WorkFlowDtos.WorkFlow
{
    public class WorkFlowGetDto
    {

        public long Id { get; set; }
        public string RequestTitle { get; set; } = null!;
        public string RequestNo { get; set; } = null!;

        public long StatuId { get; set; }
        public WorkFlowStatusGetDto? Status { get; set; }  // ilişki

        public WorkFlowPriority Priority { get; set; }
        public bool IsCancelled { get; set; }
        public bool IsComplated { get; set; }
        public WorkFlowReconciliationStatus ReconciliationStatus { get; set; }

        public DateTimeOffset CreatedDate { get; set; }
        public DateTimeOffset? UpdatedDate { get; set; }

        // ASP.NET Identity defaultu string olduğu için string bıraktım (username veya userId tutabilirsiniz)
        public long CreatedUser { get; set; }
        public long? UpdatedUser { get; set; }
        public bool IsDeleted { get; set; }
    }
}
