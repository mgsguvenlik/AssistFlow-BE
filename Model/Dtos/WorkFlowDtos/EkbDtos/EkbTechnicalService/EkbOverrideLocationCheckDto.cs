namespace Model.Dtos.WorkFlowDtos.EkbDtos.EkbTechnicalService
{
    public class EkbOverrideLocationCheckDto
    {
        public string RequestNo { get; set; } = default!;
        public string? TechnicianLatitude { get; set; }
        public string? TechnicianLongitude { get; set; }
        public string? Reason { get; set; }
    }
}
