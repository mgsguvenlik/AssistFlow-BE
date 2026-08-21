namespace Model.Dtos.OvertimeReport
{
    public class QnbTechnicianOvertimeReportDto
    {
        public long TechnicianId { get; set; }
        public string? Name { get; set; }
        public string? Code { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public double TotalOvertimeHours { get; set; }
        public int TotalJobs { get; set; }
        public List<QnbOvertimeJobDto> Jobs { get; set; } = new();
    }
}