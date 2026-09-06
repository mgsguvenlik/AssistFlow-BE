namespace Model.Dtos.OvertimeReport
{
    /// <summary>
    /// EKB - Fazla mesai iş detayı
    /// </summary>
    public class EkbOvertimeJobDto
    {
        public string RequestNo { get; set; } = string.Empty;
        public string RequestTitle { get; set; } = string.Empty;
        public DateOnly Date { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public double TotalWorkingHours { get; set; }
        public double NormalHours { get; set; }
        public double OvertimeHours { get; set; }

        /// <summary>
        /// Detaylı breakdown (hangi politikaya göre, ne kadar süre)
        /// </summary>
        public List<OvertimeBreakdownDto> OvertimeBreakdown { get; set; } = new();

        public string? StartLocation { get; set; }
        public string? EndLocation { get; set; }

        // EKB Müşteri bilgileri
        public long? EkbCustomerFormId { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerAddress { get; set; }
        public string? CustomerCity { get; set; }
        public string? ServiceTypeName { get; set; }
    }
}