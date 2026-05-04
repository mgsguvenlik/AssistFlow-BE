namespace Model.Dtos.WorkFlowDtos.QnbDtos.QnbTechnicalService
{
    public class QnbOverrideLocationCheckDto
    {
        public string RequestNo { get; set; } = default!;
        public string? TechnicianLatitude { get; set; }
        public string? TechnicianLongitude { get; set; }
        public string? Reason { get; set; }
    }
}