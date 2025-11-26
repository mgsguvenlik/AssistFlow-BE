namespace Model.Dtos.WorkFlowDtos.Warehouse
{
    public class SendWarehouseDto
    {
        public required string RequestNo { get; set; }
        public DateTimeOffset DeliveryDate { get; set; }
    }
}
