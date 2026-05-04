namespace Model.Dtos.WorkFlowDtos.QnbDtos.QnbWarehouse
{
    public class QnbSendWarehouseDto
    {
        public required string RequestNo { get; set; }
        public DateTimeOffset DeliveryDate { get; set; }
    }
}