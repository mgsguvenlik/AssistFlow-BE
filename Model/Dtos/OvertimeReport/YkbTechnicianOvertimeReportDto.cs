namespace Model.Dtos.OvertimeReport
{
    /// <summary>
    /// YKB - Teknisyen bazl� fazla mesai raporu
    /// </summary>
    public class YkbTechnicianOvertimeReportDto
    {
        public long TechnicianId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public double TotalOvertimeHours { get; set; }
        public int TotalJobs { get; set; }
        public List<YkbOvertimeJobDto> Jobs { get; set; } = new();
    }
}