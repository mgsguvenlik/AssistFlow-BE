namespace Model.Dtos.WorkFlowDtos.Warehouse
{
    public class SendWarehouseDto
    {
        public required string RequestNo { get; set; }
        public DateTimeOffset DeliveryDate { get; set; }
        public long? ApproverTechnicianId { get; set; }
        public string? Description { get; set; }
    }
}
