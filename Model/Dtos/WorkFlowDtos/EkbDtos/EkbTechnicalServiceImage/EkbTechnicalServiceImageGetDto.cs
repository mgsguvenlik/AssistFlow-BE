namespace Model.Dtos.WorkFlowDtos.EkbDtos.EkbTechnicalServiceImage
{
    public class EkbTechnicalServiceImageGetDto
    {
        public long Id { get; set; }
        public long EkbTechnicalServiceId { get; set; }
        public string Url { get; set; } = string.Empty;
        public string? Caption { get; set; }
    }
}
