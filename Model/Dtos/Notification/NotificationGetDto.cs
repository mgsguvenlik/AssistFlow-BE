using Core.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.Notification
{
    public class NotificationGetDto
    {
        public long Id { get; set; }
        public NotificationType Type { get; set; }
        public NotificationScope Scope { get; set; }
        public long? TargetUserId { get; set; }
        public string? TargetRoleCode { get; set; }

        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;

        public string? RequestNo { get; set; }
        public string? FromStepCode { get; set; }
        public string? ToStepCode { get; set; }
        public string? ReviewNotes { get; set; }
        public string? PayloadJson { get; set; }

        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }

        public DateTime CreatedDate { get; set; }
        public long? CreatedUser { get; set; }
    }
}
