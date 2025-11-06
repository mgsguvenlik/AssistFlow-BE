using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.MailOutbox
{
    public class MailOutboxCreateDto
    {
        public string RequestNo { get; set; } = "";
        public string? FromStepCode { get; set; }
        public string? ToStepCode { get; set; }

        public string ToRecipients { get; set; } = "";  // ";" ile çoklu
        public string? CcRecipients { get; set; }
        public string Subject { get; set; } = "";
        public string BodyHtml { get; set; } = "";

        public int? MaxTry { get; set; } = 5;
        public DateTime? NextAttemptAt { get; set; } = DateTime.Now;
    }
}
