namespace Model.Dtos.OvertimeReport
{
    /// <summary>
    /// Fazla mesai raporu query parametreleri
    /// </summary>
    public class OvertimeReportQueryDto
    {
        public long? TechnicianId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IncludeCustomerDetails { get; set; } = false;
    }
}