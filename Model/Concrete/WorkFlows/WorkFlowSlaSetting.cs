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

        /// <summary>SLA süresi (gün cinsinden)</summary>
        public int SlaDurationDays { get; set; }

        /// <summary>SLA bitiþ süresinden kaç gün önce bildirim gönderilecek</summary>
        public int NotificationBeforeDays { get; set; }

        /// <summary>Bildirim gönderilecek e-posta adresleri (virgülle ayrýlmýþ)</summary>
        public string? NotificationEmails { get; set; }


        /// <summary>Aktif mi</summary>
        public bool IsActive { get; set; }

        /// <summary>Açýklama/Not</summary>
        public string? Description { get; set; }
    }
}