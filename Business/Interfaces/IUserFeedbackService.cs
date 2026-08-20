using Core.Common;
using Microsoft.AspNetCore.Http;
using Model.Dtos.UserFeedbackDtos;

namespace Business.Interfaces
{
    /// <summary>
    /// Kullanıcı geri bildirimleri servisi
    /// </summary>
    public interface IUserFeedbackService
    {
        /// <summary>
        /// Yeni geri bildirim oluşturur
        /// </summary>
        Task<ResponseModel<UserFeedbackDto>> CreateFeedbackAsync(CreateUserFeedbackDto dto, string? userAgent = null);

        /// <summary>
        /// Geri bildirim listesini getirir (sayfalama ile)
        /// </summary>
        Task<ResponseModel<PaginatedList<UserFeedbackDto>>> GetFeedbacksAsync(
            int page = 1,
            int pageSize = 20,
            string? search = null,
            Core.Enums.FeedbackStatus? status = null,
            Core.Enums.FeedbackType? type = null);

        /// <summary>
        /// Belirli bir geri bildirimi getirir
        /// </summary>
        Task<ResponseModel<UserFeedbackDto>> GetFeedbackByIdAsync(long id);

        /// <summary>
        /// Geri bildirim durumunu günceller
        /// </summary>
        Task<ResponseModel<UserFeedbackDto>> UpdateFeedbackStatusAsync(long id, UpdateFeedbackStatusDto dto);

        /// <summary>
        /// Geri bildirimi siler (soft delete)
        /// </summary>
        Task<ResponseModel<bool>> DeleteFeedbackAsync(long id);

        /// <summary>
        /// Geri bildirim istatistiklerini getirir
        /// </summary>
        Task<ResponseModel<FeedbackStatisticsDto>> GetStatisticsAsync();

        /// <summary>
        /// Kullanıcının kendi geri bildirimlerini getirir
        /// </summary>
        Task<ResponseModel<List<UserFeedbackDto>>> GetMyFeedbacksAsync();

        Task<ResponseModel<List<UserFeedbackAttachmentDto>>> AddAttachmentsAsync(
            long feedbackId,
            IReadOnlyCollection<IFormFile> files,
            CancellationToken cancellationToken = default);

        Task<ResponseModel<List<UserFeedbackAttachmentDto>>> GetAttachmentsAsync(
            long feedbackId,
            CancellationToken cancellationToken = default);

        Task<ResponseModel<UserFeedbackAttachmentDto>> GetAttachmentDownloadAsync(
            long attachmentId,
            CancellationToken cancellationToken = default);

        Task<ResponseModel<bool>> DeleteAttachmentAsync(
            long attachmentId,
            CancellationToken cancellationToken = default);
    }
}
