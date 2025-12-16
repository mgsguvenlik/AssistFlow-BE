using Core.Enums;

namespace Model.Concrete
{
    public class MailOutbox // EF: MailOutboxes
    {
        public long Id { get; set; }

        // İş bağlamı
        public string RequestNo { get; set; } = "";
        public string? FromStepCode { get; set; }
        public string? ToStepCode { get; set; }

        // Mail içeriği
        public string ToRecipients { get; set; } = "";      // ; ile ayrılmış
        public string? CcRecipients { get; set; }
        public string Subject { get; set; } = "";
        public string BodyHtml { get; set; } = "";

        // Durum/deneme
        public MailOutboxStatus Status { get; set; } = MailOutboxStatus.Pending;
        public int TryCount { get; set; } = 0;
        public int MaxTry { get; set; } = 5;
        public DateTime? NextAttemptAt { get; set; }        // backoff için
        public string? LastError { get; set; }

        // Audit
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public long? CreatedUser { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public long? UpdatedUser { get; set; }
    }
}