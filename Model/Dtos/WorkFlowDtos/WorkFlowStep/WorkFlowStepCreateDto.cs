namespace Model.Dtos.WorkFlowDtos.WorkFlowStep
{
    public class WorkFlowStepCreateDto
    {
        public string? Name { get; set; }   // null gelirse mevcut değer kalsın (Mapster IgnoreNullValues)
        public string? Code { get; set; }
    }
}
