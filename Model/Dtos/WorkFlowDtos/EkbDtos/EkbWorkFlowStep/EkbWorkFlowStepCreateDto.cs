namespace Model.Dtos.WorkFlowDtos.EkbDtos.EkbWorkFlowStep
{
    public class EkbWorkFlowStepCreateDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Code { get; set; }
        public int Order { get; set; }
    }
}
