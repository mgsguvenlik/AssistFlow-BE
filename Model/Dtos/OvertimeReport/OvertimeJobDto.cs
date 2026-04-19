namespace Model.Dtos.OvertimeReport
{
    /// <summary>
    /// Fazla mesai iş detayı
    /// </summary>
    public class OvertimeJobDto
    {
        public string RequestNo { get; set; } = string.Empty;
        public string RequestTitle { get; set; } = string.Empty;
        public DateOnly Date { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public double TotalWorkingHours { get; set; }
        public double NormalHours { get; set; }
        public double OvertimeHours { get; set; }
        
        
        // Detaylı breakdown
        public List<OvertimeBreakdownDto> OvertimeBreakdown { get; set; } = new();
        
        public string? StartLocation { get; set; }
        public string? EndLocation { get; set; }

        // Müşteri bilgileri (IncludeCustomerDetails = true ise doldurulur)
        public long? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerAddress { get; set; }
        public string? CustomerCity { get; set; }
        public string? ServiceTypeName { get; set; }
    }
}