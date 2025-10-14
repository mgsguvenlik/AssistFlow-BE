namespace Model.Dtos.WorkFlowDtos.ServicesRequestProduct
{
    public class ServicesRequestProductGetDto
    {
        public long ServicesRequestId { get; set; }
        public long ProductId { get; set; }
        public decimal ProductPrice { get; set; }
        public decimal EffectivePrice { get; set; }
        public string? ProductName { get; set; }
        public string? ProductCode { get; set; }
        public long? WarehouseId { get; set; }
        public int Quantity { get; set; }
        // Toplam Fiyat (Quantity * Product.Price)
        public decimal TotalPrice { get; set; }
    }
}
