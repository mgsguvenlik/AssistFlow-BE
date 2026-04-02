using System.ComponentModel.DataAnnotations;
using Core.Enums;

namespace Model.Dtos.UserFeedbackDtos
{
    /// <summary>
    /// Geri bildirim durumu güncelleme DTO
    /// </summary>
    public class UpdateFeedbackStatusDto
    {
        /// <summary>
        /// Yeni durum
        /// </summary>
        [Required]
        public FeedbackStatus Status { get; set; }

        /// <summary>
        /// Yönetici yanıtı/notu
        /// </summary>
        [MaxLength(2000)]
        public string? AdminResponse { get; set; }

        /// <summary>
        /// Öncelik seviyesi (1-5)
        /// </summary>
        [Range(1, 5)]
        public int? Priority { get; set; }
    }
}