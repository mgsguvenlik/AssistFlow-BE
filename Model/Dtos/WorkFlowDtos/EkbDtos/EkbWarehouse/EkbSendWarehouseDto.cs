namespace Model.Dtos.WorkFlowDtos.EkbDtos.EkbWarehouse
{
    public class EkbSendWarehouseDto
    {
        public required string RequestNo { get; set; }
        public DateTimeOffset DeliveryDate { get; set; }
    }
}
