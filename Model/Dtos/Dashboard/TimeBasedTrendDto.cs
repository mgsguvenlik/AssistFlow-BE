using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.Dashboard
{
    public class TimeBasedTrendDto
    {
        // Günlük/Haftalık/Aylık Trend
        public List<TrendDataPoint> DailyTrend { get; set; } = new();
        public List<TrendDataPoint> WeeklyTrend { get; set; } = new();
        public List<TrendDataPoint> MonthlyTrend { get; set; } = new();
    }
    
    public class TrendDataPoint
    {
        public DateTime Date { get; set; }
        public string Period { get; set; } = string.Empty; // "2024-01-15", "Week 3", "Jan 2024"
        public int CreatedCount { get; set; }
        public int CompletedCount { get; set; }
        public int CancelledCount { get; set; }
        public decimal TotalRevenueTL { get; set; }
        public decimal TotalRevenueUSD { get; set; }
    }
}
