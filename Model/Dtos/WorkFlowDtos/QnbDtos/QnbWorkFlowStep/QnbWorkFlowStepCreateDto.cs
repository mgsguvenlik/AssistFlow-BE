namespace Model.Dtos.WorkFlowDtos.QnbDtos.QnbWorkFlowStep
{
    public class QnbWorkFlowStepCreateDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Code { get; set; }
        public int Order { get; set; }
    }
}