using Core.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.Notification
{
    public class NotificationCreateDto
    {
        public NotificationType Type { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;

        public string? RequestNo { get; set; }
        public string? FromStepCode { get; set; }
        public string? ToStepCode { get; set; }
        public string? ReviewNotes { get; set; }
        public object? Payload { get; set; }             // JSON’a serileyeceğiz

        // hedefler
        public long? TargetUserId { get; set; }          // tek kullanıcı
        public List<long>? TargetUserIds { get; set; }   // çoklu kullanıcı
        public string? TargetRoleCode { get; set; }      // tek rol
        public List<string>? TargetRoleCodes { get; set; } // çoklu rol
    }

}
