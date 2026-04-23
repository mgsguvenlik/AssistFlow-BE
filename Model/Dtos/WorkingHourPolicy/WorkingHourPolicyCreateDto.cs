using Model.Concrete;
using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.WorkingHourPolicy
{
    public class WorkingHourPolicyCreateDto
    {
        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [Required]
        public WorkingHourPolicyType PolicyType { get; set; }

        public DateOnly? SpecificDate { get; set; }
        public int? Year { get; set; }
        public DayOfWeek? DayOfWeek { get; set; }
        public TimeOnly? WorkStartTime { get; set; }
        public TimeOnly? WorkEndTime { get; set; }
        public bool IsActive { get; set; } = true;
        public int Priority { get; set; } = 0;
        public string CountryCode { get; set; } = "TR";
        public bool IsPublicHoliday { get; set; } = false;
    }
}