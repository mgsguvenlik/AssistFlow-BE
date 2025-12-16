namespace Model.Dtos.WorkFlowDtos.YkbDtos.YkbTechnicalServiceImage
{
    public class YkbTechnicalServiceImageCreateDto
    {
        public long YkbTechnicalServiceId { get; set; }
        public string Url { get; set; } = string.Empty;
        public string? Caption { get; set; }
    }
}
