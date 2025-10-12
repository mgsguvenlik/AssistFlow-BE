namespace Model.Dtos.WorkFlowDtos.Warehouse
{
    public class SendWarehouseDto
    {
        public required string RequestNo { get; set; }
        public long ServicesRequestId { get; set; }
        public DateTimeOffset DeliveryDate { get; set; }
        public long? ApproverTechnicianId { get; set; }
        public string? Description { get; set; }
        public List<long>? ProductIds { get; set; }
    }
}
