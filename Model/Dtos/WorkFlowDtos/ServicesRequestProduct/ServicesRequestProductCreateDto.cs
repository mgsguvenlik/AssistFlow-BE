namespace Model.Dtos.WorkFlowDtos.ServicesRequestProduct
{
    public class ServicesRequestProductCreateDto
    {
        public long ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal? Price { get; set; }
    }
}
