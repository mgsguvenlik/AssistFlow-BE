namespace Model.Dtos.WorkFlowDtos.YkbDtos.YkbServicesRequestProduct
{
    public class YkbServicesRequestProductCreateDto
    {
        public long ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal? Price { get; set; }

        /// <summary>
        /// Kullanıcı özel fiyatı uygulamak istiyor mu?
        /// </summary>
        public bool ApplyPriceAdjustment { get; set; }

        /// <summary>
        /// Kullanıcının girdiği yüzde veya tutar.
        /// </summary>
        public decimal? PriceAdjustmentValue { get; set; }
    }
}
