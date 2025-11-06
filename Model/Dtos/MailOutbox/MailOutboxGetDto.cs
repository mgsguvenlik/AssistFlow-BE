using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model.Dtos.MailOutbox
{
    public class MailOutboxGetDto
    {
        public long Id { get; set; }

        public string RequestNo { get; set; } = "";
        public string? FromStepCode { get; set; }
        public string? ToStepCode { get; set; }

        public string ToRecipients { get; set; } = "";
        public string? CcRecipients { get; set; }
        public string Subject { get; set; } = "";
        public string BodyHtml { get; set; } = "";

        public int Status { get; set; }
        public int TryCount { get; set; }
        public int MaxTry { get; set; }
        public DateTime? NextAttemptAt { get; set; }
        public string? LastError { get; set; }

        public DateTime CreatedDate { get; set; }
        public long? CreatedUser { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public long? UpdatedUser { get; set; }
    }
}
