namespace Model.Dtos.OvertimeReport
{
    public class QnbTechnicianOvertimeReportDto
    {
        public long TechnicianId { get; set; }
        public string? TechnicianName { get; set; }
        public string? TechnicianCode { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public double TotalOvertimeHours { get; set; }
        public int TotalJobs { get; set; }
        public List<QnbOvertimeJobDto> Jobs { get; set; } = new();
    }
}