namespace Model.Dtos.WorkFlowDtos.YkbDtos.YkbWorkFlowStep
{
    public class YkbWorkFlowStepCreateDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Code { get; set; }
        public int Order { get; set; }
    }
}
