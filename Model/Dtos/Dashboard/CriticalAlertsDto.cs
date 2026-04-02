using Core.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.Dashboard
{
    public class CriticalAlertsDto
    {
        // Gecikmeler
        public List<DelayedWorkFlowDto> DelayedWorkFlows { get; set; } = new();
        
        // Lokasyon Sorunları
        public List<LocationIssueDto> LocationIssues { get; set; } = new();
        
        // Geri Gönderilenler
        public List<ReviewBackDto> RecentReviewBacks { get; set; } = new();
        
        // Bekleyen Onaylar
        public int PendingFinalApprovals { get; set; }
        public int PendingPricingApprovals { get; set; }
        public int PendingWarehouseDeliveries { get; set; }
        
        // Kritik Öncelikli İşler
        public int CriticalPriorityPending { get; set; }
        public int HighPriorityPending { get; set; }
    }
    
    public class DelayedWorkFlowDto
    {
        public long Id { get; set; }
        public string RequestNo { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CurrentStep { get; set; } = string.Empty;
        public int DelayHours { get; set; }
        public int DelayDays { get; set; }
        public WorkFlowPriority Priority { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? PlannedCompletionDate { get; set; }
    }
    
    public class LocationIssueDto
    {
        public string RequestNo { get; set; } = string.Empty;
        public string TechnicianName { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public DateTime IssueDate { get; set; }
        public string IssueType { get; set; } = string.Empty; // "Override Request", "Failed Check"
        public double? DistanceKm { get; set; }
    }
    
    public class ReviewBackDto
    {
        public string RequestNo { get; set; } = string.Empty;
        public string FromStep { get; set; } = string.Empty;
        public string ToStep { get; set; } = string.Empty;
        public string ReviewNotes { get; set; } = string.Empty;
        public DateTime ReviewDate { get; set; }
        public long? ReviewedBy { get; set; }
    }
}
