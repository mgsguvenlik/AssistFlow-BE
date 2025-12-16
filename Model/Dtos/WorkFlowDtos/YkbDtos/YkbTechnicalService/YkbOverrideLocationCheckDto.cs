namespace Model.Dtos.WorkFlowDtos.YkbDtos.YkbTechnicalService
{
    public class YkbOverrideLocationCheckDto
    {
        public string RequestNo { get; set; } = default!;
        public string? TechnicianLatitude { get; set; }
        public string? TechnicianLongitude { get; set; }
        public string? Reason { get; set; }
    }
}
