using System.ComponentModel.DataAnnotations;
using Core.Enums;

namespace Model.Dtos.UserFeedbackDtos
{
    /// <summary>
    /// Kullanıcı geri bildirimi oluşturma DTO
    /// </summary>
    public class CreateUserFeedbackDto
    {
        /// <summary>
        /// Geri bildirim başlığı
        /// </summary>
        [Required(ErrorMessage = "Başlık zorunludur")]
        [MaxLength(250, ErrorMessage = "Başlık en fazla 250 karakter olabilir")]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Detaylı açıklama
        /// </summary>
        [Required(ErrorMessage = "Açıklama zorunludur")]
        [MaxLength(5000, ErrorMessage = "Açıklama en fazla 5000 karakter olabilir")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Geri bildirim tipi
        /// </summary>
        [Required]
        public FeedbackType FeedbackType { get; set; }

        /// <summary>
        /// İlgili URL
        /// </summary>
        [MaxLength(500)]
        public string? RelatedUrl { get; set; }

        /// <summary>
        /// Ek dosya URL'leri
        /// </summary>
        public List<string>? AttachmentUrls { get; set; }
    }
}