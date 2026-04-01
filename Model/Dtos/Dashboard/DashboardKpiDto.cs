using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.Dashboard
{
    public class DashboardKpiDto
    {
        // Genel İstatistikler
        public int TotalActiveWorkFlows { get; set; }
        public int TotalCompletedWorkFlows { get; set; }
        public int TotalCancelledWorkFlows { get; set; }
        public int TotalPendingWorkFlows { get; set; }
        
        // Adım Bazlı Dağılım
        public int InServiceRequest { get; set; }      // SR
        public int InWarehouse { get; set; }            // WH
        public int InTechnicalService { get; set; }     // TS
        public int InPricing { get; set; }              // PRC
        public int InFinalApproval { get; set; }        // APR
        
        // Bugün/Bu Ay
        public int CreatedToday { get; set; }
        public int CompletedToday { get; set; }
        public int CreatedThisMonth { get; set; }
        public int CompletedThisMonth { get; set; }
        
        // Zaman Metrikleri
        public double AverageCompletionTimeHours { get; set; }
        public double AverageTechnicalServiceTimeHours { get; set; }
        
        // Öncelik Dağılımı
        public int LowPriorityCount { get; set; }
        public int NormalPriorityCount { get; set; }
        public int HighPriorityCount { get; set; }
        public int CriticalPriorityCount { get; set; }
    }
}
