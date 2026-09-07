namespace Model.Dtos.OvertimeReport
{
    /// <summary>
    /// Teknisyen bazlı fazla mesai raporu
    /// </summary>
    public class TechnicianOvertimeReportDto
    {
        public long TechnicianId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public double TotalOvertimeHours { get; set; }
        public int TotalJobs { get; set; }
        public List<OvertimeJobDto> Jobs { get; set; } = new();
    }
}