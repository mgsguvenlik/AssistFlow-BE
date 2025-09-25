using Model.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Concrete
{
    public class WorkFlow : AuditableWithUserEntity
    {
        public long Id { get; set; }              
        public DateOnly Date { get; set; }       
        public bool Agreed { get; set; }

        // Navigations
        public long WorkFlowStatusId { get; set; } 
        public WorkFlowStatus? WorkFlowStatus { get; set; }
        public ICollection<WorkflowHistory> History { get; set; } = new List<WorkflowHistory>();
        public ICollection<WorkFlowAnswer> Answers { get; set; } = new List<WorkFlowAnswer>();
    }
}
