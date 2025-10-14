namespace Model.Dtos.WorkFlowDtos.ServicesRequestProduct
{
    public class ServicesRequestProductUpdateDto
    {
        public long ProductId { get; set; }
        public long? WarehouseId { get; set; }
        public int Quantity { get; set; }
    }
}
