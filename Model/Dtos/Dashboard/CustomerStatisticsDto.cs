using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.Dashboard
{
    public class CustomerStatisticsDto
    {
        public long CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? CustomerCode { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
        
        // İş Sayıları
        public int TotalRequests { get; set; }
        public int CompletedRequests { get; set; }
        public int ActiveRequests { get; set; }
        public int CancelledRequests { get; set; }
        
        // Finansal
        public decimal TotalServiceCostTL { get; set; }
        public decimal TotalServiceCostUSD { get; set; }
        
        // Garanti Durumu
        public int InWarrantyCount { get; set; }
        public int OutOfWarrantyCount { get; set; }
        
        // Son İşlem
        public DateTime? LastServiceDate { get; set; }
    }
}
