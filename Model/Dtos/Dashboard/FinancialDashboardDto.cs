using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.Dashboard
{
    public class FinancialDashboardDto
    {
        // Toplam Cirolar
        public decimal TotalRevenueTL { get; set; }
        public decimal TotalRevenueUSD { get; set; }
        
        // Bu Ay
        public decimal MonthlyRevenueTL { get; set; }
        public decimal MonthlyRevenueUSD { get; set; }
        
        // Bu Hafta
        public decimal WeeklyRevenueTL { get; set; }
        public decimal WeeklyRevenueUSD { get; set; }
        
        // Bugün
        public decimal DailyRevenueTL { get; set; }
        public decimal DailyRevenueUSD { get; set; }
        
        // Fiyatlama Durumları
        public int PendingPricing { get; set; }
        public int ApprovedPricing { get; set; }
        public int RejectedPricing { get; set; }
        
        // Maliyet Tipleri
        public int WarrantyServices { get; set; }
        public int PaidServices { get; set; }
        public int FreeServices { get; set; }
        public int UnknownCostServices { get; set; }
        
        // Ortalama İş Değeri
        public decimal AverageJobValueTL { get; set; }
        public decimal AverageJobValueUSD { get; set; }
        
        // İndirim İstatistikleri
        public decimal TotalDiscountAmount { get; set; }
        public decimal AverageDiscountPercent { get; set; }
    }
}
