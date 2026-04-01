using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.Dashboard
{
    public class StepDurationAnalysisDto
    {
        public string StepCode { get; set; } = string.Empty;
        public string StepName { get; set; } = string.Empty;
        
        // Süre Metrikleri (saat cinsinden)
        public double AverageDuration { get; set; }
        public double MinDuration { get; set; }
        public double MaxDuration { get; set; }
        public double MedianDuration { get; set; }
        
        // İşlem Sayıları
        public int TotalProcessed { get; set; }
        public int CurrentlyInStep { get; set; }
    }
}
