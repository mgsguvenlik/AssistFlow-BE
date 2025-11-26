using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.MailOutbox
{

    public class MailOutboxUpdateDto
    {
        public long Id { get; set; }

        public string RequestNo { get; set; } = "";
        public string? FromStepCode { get; set; }
        public string? ToStepCode { get; set; }

        public string ToRecipients { get; set; } = "";
        public string? CcRecipients { get; set; }
        public string Subject { get; set; } = "";
        public string BodyHtml { get; set; } = "";

        public int MaxTry { get; set; } = 5;
        public DateTime? NextAttemptAt { get; set; }
        public int TryCount { get; set; }           // istersen güncellenebilir bırakma
        public int Status { get; set; }             // enum int (MailOutboxStatus)
        public string? LastError { get; set; }
    }

}
