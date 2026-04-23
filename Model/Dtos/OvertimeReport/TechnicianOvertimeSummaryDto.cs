namespace Model.Dtos.OvertimeReport
{
    /// <summary>
    /// Teknisyen özet bilgisi
    /// </summary>
    public class TechnicianOvertimeSummaryDto
    {
        public long TechnicianId { get; set; }
        public string TechnicianName { get; set; } = string.Empty;
        public string TechnicianCode { get; set; } = string.Empty;
        public double TotalOvertimeHours { get; set; }
        public int TotalJobs { get; set; }
        public List<string> RequestNos { get; set; } = new();
    }
}