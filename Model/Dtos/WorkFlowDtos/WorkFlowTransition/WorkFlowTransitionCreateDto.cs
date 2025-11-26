using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.WorkFlowDtos.WorkFlowTransition
{
    public class WorkFlowTransitionCreateDto
    {
        [Required]
        public long FromStepId { get; set; }

        [Required]
        public long ToStepId { get; set; }

        [Required, MaxLength(100)]
        public string TransitionName { get; set; } = string.Empty;

        public string? Condition { get; set; }
    }

}
