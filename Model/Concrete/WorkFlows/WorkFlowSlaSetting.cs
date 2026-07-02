using Core.Enums;
using Model.Abstractions;

namespace Model.Concrete.WorkFlows
{
    public class WorkFlowSlaSetting : AuditableWithUserEntity
    {
        public long Id { get; set; }
        /// <summary>Müþteri/Ýþ birimi tipi</summary>
        public WorkFlowCustomerType CustomerType { get; set; } = WorkFlowCustomerType.Individual;
        /// <summary>Ýþ akýþý öncelik seviyesi</summary>
        public WorkFlowPriority Priority { get; set; }
        /// <summary>SLA süresi (saat cinsinden)</summary>
        public int SlaDurationHours { get; set; }
        /// <summary>SLA bitiþ süresinden kaç saat önce bildirim gönderilecek</summary>
        public int NotificationBeforeHours { get; set; }
        /// <summary>Bildirim gönderilecek e-posta adresleri (virgülle ayrýlmýþ)</summary>
        public string? NotificationEmails { get; set; }
        /// <summary>Aktif mi</summary>
        public bool IsActive { get; set; }
        /// <summary>Açýklama/Not</summary>
        public string? Description { get; set; }
    }
}