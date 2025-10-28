using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.WorkFlowDtos.WorkFlowTransition
{
    public class WorkFlowTransitionUpdateDto
    {
        [Required]
        public long Id { get; set; }

        [Required]
        public long FromStepId { get; set; }

        [Required]
        public long ToStepId { get; set; }

        [Required, MaxLength(100)]
        public string TransitionName { get; set; } = string.Empty;

        public string? Condition { get; set; }
    }
}
