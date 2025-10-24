namespace Model.Dtos.WorkFlowDtos.TechnicalService
{
    public sealed class OverrideLocationCheckDto
    {
        public string RequestNo { get; set; } = default!;
        public string? TechnicianLatitude { get; set; }
        public string? TechnicianLongitude { get; set; }
        public string? Reason { get; set; }
    }
}
