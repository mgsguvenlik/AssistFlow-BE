namespace Model.Dtos.WorkFlowDtos.QnbDtos.QnbWorkFlowStep
{
    public class QnbWorkFlowStepGetDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Code { get; set; }
        public int Order { get; set; }
    }
}