using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.Dashboard
{
    public class TechnicianPerformanceDto
    {
        public long TechnicianId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? City { get; set; }
        
        // İş Yükü
        public int ActiveTasksCount { get; set; }
        public int CompletedTasksCount { get; set; }
        public int TotalTasksCount { get; set; }
        
        // Performans
        public double AverageCompletionTimeHours { get; set; }
        public double CompletionRate { get; set; } // %
        
        // Lokasyon Başarısı
        public int LocationCheckFailures { get; set; }
        public int LocationOverrideRequests { get; set; }
        
        // Geri Gönderimler
        public int ReviewBackCount { get; set; }
        
        // Bu Ay
        public int CompletedThisMonth { get; set; }
    }
}
