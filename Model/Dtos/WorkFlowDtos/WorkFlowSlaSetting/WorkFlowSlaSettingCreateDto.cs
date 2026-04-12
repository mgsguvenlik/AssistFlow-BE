using Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace Model.Dtos.WorkFlowDtos.WorkFlowSlaSetting
{
    public class WorkFlowSlaSettingCreateDto
    {
        [Required(ErrorMessage = "Müþteri tipi zorunludur.")]
        public WorkFlowCustomerType CustomerType { get; set; }

        [Required(ErrorMessage = "Öncelik alaný zorunludur.")]
        public WorkFlowPriority Priority { get; set; }

        [Required(ErrorMessage = "SLA süresi zorunludur.")]
        [Range(1, 365, ErrorMessage = "SLA süresi 1-365 gün arasýnda olmalýdýr.")]
        public int SlaDurationDays { get; set; }

        [Required(ErrorMessage = "Bildirim süresi zorunludur.")]
        [Range(1, 365, ErrorMessage = "Bildirim süresi 1-365 gün arasýnda olmalýdýr.")]
        public int NotificationBeforeDays { get; set; }

        [EmailAddressList(ErrorMessage = "Geçerli e-posta adresleri giriniz (virgülle ayrýlmýþ).")]
        public string? NotificationEmails { get; set; }

        public bool IsActive { get; set; } = true;

        public string? Description { get; set; }
    }

    public class EmailAddressListAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
                return ValidationResult.Success;

            var emails = value.ToString()!.Split(',', StringSplitOptions.RemoveEmptyEntries);
            var emailValidator = new EmailAddressAttribute();

            foreach (var email in emails)
            {
                if (!emailValidator.IsValid(email.Trim()))
                {
                    return new ValidationResult($"Geçersiz e-posta adresi: {email.Trim()}");
                }
            }

            return ValidationResult.Success;
        }
    }
}