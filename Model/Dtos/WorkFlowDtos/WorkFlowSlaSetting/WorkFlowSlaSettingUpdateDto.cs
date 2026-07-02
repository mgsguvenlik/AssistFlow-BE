using Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.WorkFlowDtos.WorkFlowSlaSetting
{
    public class WorkFlowSlaSettingUpdateDto
    {
        [Required(ErrorMessage = "Id alaný zorunludur.")]
        public long Id { get; set; }
        [Required(ErrorMessage = "Müþteri tipi zorunludur.")]
        public WorkFlowCustomerType CustomerType { get; set; }
        [Required(ErrorMessage = "Öncelik alaný zorunludur.")]
        public WorkFlowPriority Priority { get; set; }
        [Required(ErrorMessage = "SLA süresi zorunludur.")]
        [Range(1, 8760, ErrorMessage = "SLA süresi 1-8760 saat (1-365 gün) arasýnda olmalýdýr.")]
        public int SlaDurationHours { get; set; }
        [Required(ErrorMessage = "Bildirim süresi zorunludur.")]
        [Range(1, 8760, ErrorMessage = "Bildirim süresi 1-8760 saat (1-365 gün) arasýnda olmalýdýr.")]
        public int NotificationBeforeHours { get; set; }
        [EmailAddressList(ErrorMessage = "Geçerli e-posta adresleri giriniz (virgülle ayrýlmýþ).")]
        public string? NotificationEmails { get; set; }
        public bool IsActive { get; set; }
        public string? Description { get; set; }
    }
}