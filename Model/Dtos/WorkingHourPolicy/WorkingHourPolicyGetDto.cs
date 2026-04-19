using Model.Concrete;

namespace Model.Dtos.WorkingHourPolicy
{
    public class WorkingHourPolicyGetDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public WorkingHourPolicyType PolicyType { get; set; }
        public string PolicyTypeText { get; set; } = string.Empty;
        public DateOnly? SpecificDate { get; set; }
        public int? Year { get; set; }
        public DayOfWeek? DayOfWeek { get; set; }
        public string? DayOfWeekText { get; set; }
        public TimeOnly? WorkStartTime { get; set; }
        public TimeOnly? WorkEndTime { get; set; }
        public bool IsActive { get; set; }
        public int Priority { get; set; }
        public string CountryCode { get; set; } = "TR";
        public bool IsPublicHoliday { get; set; }
    }
}