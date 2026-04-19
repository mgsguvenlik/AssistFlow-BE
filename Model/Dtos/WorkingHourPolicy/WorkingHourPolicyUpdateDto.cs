namespace Model.Dtos.WorkingHourPolicy
{
    public class WorkingHourPolicyUpdateDto
    {
        public long Id { get; set; }
        public TimeOnly? WorkStartTime { get; set; }
        public TimeOnly? WorkEndTime { get; set; }
        public bool IsActive { get; set; }
        public string? Description { get; set; }
        public int Priority { get; set; }
    }
}