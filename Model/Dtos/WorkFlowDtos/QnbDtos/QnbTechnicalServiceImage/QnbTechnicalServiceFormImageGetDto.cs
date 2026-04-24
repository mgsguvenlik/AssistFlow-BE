namespace Model.Dtos.WorkFlowDtos.QnbDtos.QnbTechnicalServiceImage
{
    public class QnbTechnicalServiceFormImageGetDto
    {
        public long Id { get; set; }
        public long QnbTechnicalServiceId { get; set; }
        public string Url { get; set; } = string.Empty;
        public string? Caption { get; set; }
    }
}