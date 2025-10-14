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
        public Warehouse? Warehouse { get; set; } = default!;
        public long? WarehouseId { get; set; }
        public Customer? Customer { get; set; } = default!;  ///Müşteriye göre fiyat hesaplamak için eklendi
        public long? CustomerId { get; set; }
        public int Quantity { get; set; }

        // Toplam Fiyat (Quantity * Product.Price)
        public decimal TotalPrice => Quantity * (Product?.Price ?? 0m);


        // Müşteriye özel fiyatlandırma varsa onu, yoksa ürünün standart fiyatını döner
        public decimal GetEffectivePrice()
        {
            if (Customer?.CustomerGroup?.GroupProductPrices
                .FirstOrDefault(gp => gp.ProductId == ProductId) is { } groupPrice)
                return groupPrice.Price;

            if (Customer?.CustomerProductPrices
                .FirstOrDefault(cp => cp.ProductId == ProductId) is { } customerPrice)
                return customerPrice.Price;
            return Product?.Price ?? 0m;
        }

        // Miktar ile çarpılmış toplam efektif fiyat
        public decimal GetTotalEffectivePrice()
        {
            if (Customer?.CustomerGroup?.GroupProductPrices
                .FirstOrDefault(gp => gp.ProductId == ProductId) is { } groupPrice)
                return Quantity*groupPrice.Price;

            if (Customer?.CustomerProductPrices
                .FirstOrDefault(cp => cp.ProductId == ProductId) is { } customerPrice)
                return customerPrice.Price;
            return Quantity*Product?.Price ?? 0m;
        }

    }
}
