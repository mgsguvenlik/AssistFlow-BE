namespace Model.Dtos.OvertimeReport
{
    /// <summary>
    /// YKB - Teknisyen bazlý fazla mesai raporu
    /// </summary>
    public class YkbTechnicianOvertimeReportDto
    {
        public long TechnicianId { get; set; }
        public string TechnicianName { get; set; } = string.Empty;
        public string TechnicianCode { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public double TotalOvertimeHours { get; set; }
        public int TotalJobs { get; set; }
        public List<YkbOvertimeJobDto> Jobs { get; set; } = new();
    }
}