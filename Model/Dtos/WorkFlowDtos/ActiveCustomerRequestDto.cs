namespace Model.Dtos.WorkFlowDtos
{
    public class ActiveCustomerRequestDto
    {
        public string RequestNo { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string? Title { get; set; }
        public string? CurrentStepName { get; set; }
        public DateTimeOffset CreatedDate { get; set; }
    }
}
