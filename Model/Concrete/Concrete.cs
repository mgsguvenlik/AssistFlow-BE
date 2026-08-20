using System.ComponentModel.DataAnnotations;
using Core.Enums;
using Model.Abstractions;

namespace Model.Concrete
{
    /// <summary>
    /// Kullanıcı geri bildirimleri (öneri, talep, hata, sorun)
    /// </summary>
    public class UserFeedback : AuditableWithUserEntity
    {
        [Key]
        public long Id { get; set; }

        /// <summary>
        /// Geri bildirim başlığı
        /// </summary>
        [Required, MaxLength(250)]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Detaylı açıklama
        /// </summary>
        [Required, MaxLength(5000)]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Geri bildirim tipi (Öneri, Hata, Talep vb.)
        /// </summary>
        public FeedbackType FeedbackType { get; set; } = FeedbackType.Suggestion;

        /// <summary>
        /// Geri bildirim durumu
        /// </summary>
        public FeedbackStatus Status { get; set; } = FeedbackStatus.Created;

        /// <summary>
        /// Öncelik seviyesi (1-5, 5 en yüksek)
        /// </summary>
        public int Priority { get; set; } = 3;

        /// <summary>
        /// Yönetici notu/yanıtı
        /// </summary>
        [MaxLength(2000)]
        public string? AdminResponse { get; set; }

        /// <summary>
        /// Yanıt tarihi
        /// </summary>
        public DateTimeOffset? ResponseDate { get; set; }

        /// <summary>
        /// Yanıtlayan kullanıcı ID
        /// </summary>
        public long? RespondedBy { get; set; }

        /// <summary>
        /// Tamamlanma tarihi
        /// </summary>
        public DateTimeOffset? CompletedDate { get; set; }

        /// <summary>
        /// İlgili URL veya sayfa
        /// </summary>
        [MaxLength(500)]
        public string? RelatedUrl { get; set; }

        /// <summary>
        /// Tarayıcı/Cihaz bilgisi
        /// </summary>
        [MaxLength(500)]
        public string? UserAgent { get; set; }

        /// <summary>
        /// Ekran görüntüsü veya ek dosya URL'leri (JSON array olarak)
        /// </summary>
        [MaxLength(2000)]
        public string? AttachmentUrls { get; set; }

        /// <summary>
        /// IFileStorage üzerinde tutulan geri bildirim dosyalarının metadata kayıtları.
        /// </summary>
        public ICollection<UserFeedbackAttachment> Attachments { get; set; } =
            new List<UserFeedbackAttachment>();
    }
}
