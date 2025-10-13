using Model.Abstractions;

namespace Model.Concrete.WorkFlows
{
    // Ara tablo: N-N (ServicesRequest <-> Product)
    public class ServicesRequestProduct : BaseEntity
    {
        public long ServicesRequestId { get; set; }
        public ServicesRequest ServicesRequest { get; set; } = default!;

        public long ProductId { get; set; }
        public Product Product { get; set; } = default!;

        public Warehouse Warehouse { get; set; } = default!; // Depo (opsiyonel)
        public long? WarehouseId { get; set; }
    }

}
