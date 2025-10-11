namespace Model.Dtos.WorkFlowDtos.WorkFlowStatus
{
    public class WorkFlowStatusCreateDto
    {
        public string? Name { get; set; }   // null gelirse mevcut değer kalsın (Mapster IgnoreNullValues)
        public string? Code { get; set; }
    }
}
