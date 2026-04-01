using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.Dashboard
{
    public class ProductStatisticsDto
    {
        // En Çok Kullanılan Ürünler
        public List<ProductUsageDto> TopProducts { get; set; } = new();
        
        // Depo Durumu
        public int PendingWarehouseDeliveries { get; set; }
        public int CompletedWarehouseDeliveries { get; set; }
        public int AwaitingReviewWarehouses { get; set; }
        
        // Toplam Ürün İstatistikleri
        public int TotalProductsUsed { get; set; }
        public int TotalQuantity { get; set; }
    }
    
    public class ProductUsageDto
    {
        public long ProductId { get; set; }
        public string ProductCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public int UsageCount { get; set; }
        public int TotalQuantity { get; set; }
        public decimal TotalCostTL { get; set; }
        public decimal TotalCostUSD { get; set; }
    }
}
