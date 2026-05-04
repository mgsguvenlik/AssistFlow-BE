namespace Model.Dtos.WorkFlowDtos.QnbDtos.QnbTechnicalServiceImage
{
    public class QnbTechnicalServiceImageCreateDto
    {
        public long QnbTechnicalServiceId { get; set; }
        public string Url { get; set; } = string.Empty;
        public string? Caption { get; set; }
    }
}