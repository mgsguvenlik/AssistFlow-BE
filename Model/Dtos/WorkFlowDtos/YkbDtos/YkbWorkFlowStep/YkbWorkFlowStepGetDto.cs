namespace Model.Dtos.WorkFlowDtos.YkbDtos.YkbWorkFlowStep
{
    public class YkbWorkFlowStepGetDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Code { get; set; }
        public int Order { get; set; }
    }
}
