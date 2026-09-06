namespace Model.Dtos.OvertimeReport
{
    /// <summary>
    /// Teknisyen özet bilgisi
    /// </summary>
    public class TechnicianOvertimeSummaryDto
    {
        public long TechnicianId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public double TotalOvertimeHours { get; set; }
        public int TotalJobs { get; set; }
        public List<string> RequestNos { get; set; } = new();
    }
}