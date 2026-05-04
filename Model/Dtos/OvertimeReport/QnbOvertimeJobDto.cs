namespace Model.Dtos.OvertimeReport
{
    public class QnbOvertimeJobDto
    {
        public string RequestNo { get; set; } = default!;
        public string RequestTitle { get; set; } = default!;
        public DateOnly Date { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public double TotalWorkingHours { get; set; }
        public double NormalHours { get; set; }
        public double OvertimeHours { get; set; }
        public List<OvertimeBreakdownDto> OvertimeBreakdown { get; set; } = new();
        public string? StartLocation { get; set; }
        public string? EndLocation { get; set; }

        // Müþteri detaylarý (opsiyonel)
        public long? QnbCustomerFormId { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerAddress { get; set; }
        public string? CustomerCity { get; set; }
        public string? ServiceTypeName { get; set; }
    }
}