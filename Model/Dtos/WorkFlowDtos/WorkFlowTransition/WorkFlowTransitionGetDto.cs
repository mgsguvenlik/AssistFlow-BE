using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.WorkFlowDtos.WorkFlowTransition
{
    public class WorkFlowTransitionGetDto
    {
        public long Id { get; set; }

        public long FromStepId { get; set; }
        public string? FromStepName { get; set; }

        public long ToStepId { get; set; }
        public string? ToStepName { get; set; }

        public string TransitionName { get; set; } = string.Empty;

        public string? Condition { get; set; }
    }
}
