using Model.Abstractions;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Model.Concrete
{
    /// <summary>
    /// Kullanıcı geri bildirimlerine eklenen dosyaların metadata kaydı.
    /// Dosya içeriği IFileStorage üzerinde tutulur.
    /// </summary>
    [Table("UserFeedbackAttachments")]
    public class UserFeedbackAttachment : AuditableWithUserEntity
    {
        [Key]
        public long Id { get; set; }

        public long UserFeedbackId { get; set; }

        public UserFeedback UserFeedback { get; set; } = default!;

        [Required, MaxLength(260)]
        public string OriginalFileName { get; set; } = string.Empty;

        [Required, MaxLength(260)]
        public string StoredFileName { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string Extension { get; set; } = string.Empty;

        [Required, MaxLength(150)]
        public string ContentType { get; set; } = "application/octet-stream";

        public long SizeBytes { get; set; }
    }
}
