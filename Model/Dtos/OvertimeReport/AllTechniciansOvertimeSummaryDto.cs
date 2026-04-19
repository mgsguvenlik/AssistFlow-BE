namespace Model.Dtos.OvertimeReport
{
    /// <summary>
    /// Tüm teknisyenler fazla mesai özeti
    /// </summary>
    public class AllTechniciansOvertimeSummaryDto
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public double TotalOvertimeHours { get; set; }
        public int TotalJobs { get; set; }
        public int TotalTechnicians { get; set; }
        public List<TechnicianOvertimeSummaryDto> Technicians { get; set; } = new();
    }
}