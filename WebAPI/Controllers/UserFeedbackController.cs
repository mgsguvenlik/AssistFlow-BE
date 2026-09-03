using Business.Interfaces;
using Core.Common;
using Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.UserFeedbackDtos;
using WebAPI.Authorization;

namespace WebAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class UserFeedbackController : ControllerBase
    {
        private readonly IUserFeedbackService _feedbackService;
        private readonly ILogger<UserFeedbackController> _logger;

        public UserFeedbackController(
            IUserFeedbackService feedbackService,
            ILogger<UserFeedbackController> logger)
        {
            _feedbackService = feedbackService;
            _logger = logger;
        }

        /// <summary>
        /// Yeni geri bildirim oluşturur (öneri, talep, hata vb.)
        /// </summary>
        /// <param name="dto">Geri bildirim bilgileri</param>
        /// <returns>Oluşturulan geri bildirim</returns>
        [HttpPost]
        [MenuAuthorize("UserFeedback", MenuPermission.Edit)]
        [ProducesResponseType(typeof(ResponseModel<UserFeedbackDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ResponseModel<UserFeedbackDto>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateFeedback([FromBody] CreateUserFeedbackDto dto)
        {
            var userAgent = Request.Headers["User-Agent"].ToString();
            var result = await _feedbackService.CreateFeedbackAsync(dto, userAgent);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Geri bildirim listesini getirir (sayfalama ve filtreleme ile)
        /// </summary>
        /// <param name="page">Sayfa numarası</param>
        /// <param name="pageSize">Sayfa boyutu</param>
        /// <param name="search">Arama terimi</param>
        /// <param name="status">Durum filtresi</param>
        /// <param name="type">Tip filtresi</param>
        /// <returns>Geri bildirim listesi</returns>
        [HttpGet]
        [MenuAuthorize("UserFeedback", MenuPermission.View)]
        [ProducesResponseType(typeof(ResponseModel<PaginatedList<UserFeedbackDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetFeedbacks(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? search = null,
            [FromQuery] FeedbackStatus? status = null,
            [FromQuery] FeedbackType? type = null)
        {
            var result = await _feedbackService.GetFeedbacksAsync(page, pageSize, search, status, type);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Belirli bir geri bildirimi getirir
        /// </summary>
        /// <param name="id">Geri bildirim ID</param>
        /// <returns>Geri bildirim detayı</returns>
        [HttpGet("{id}")]
        [MenuAuthorize("UserFeedback", MenuPermission.View)]
        [ProducesResponseType(typeof(ResponseModel<UserFeedbackDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ResponseModel<UserFeedbackDto>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetFeedbackById(long id)
        {
            var result = await _feedbackService.GetFeedbackByIdAsync(id);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Geri bildirim durumunu günceller (Admin)
        /// </summary>
        /// <param name="id">Geri bildirim ID</param>
        /// <param name="dto">Güncelleme bilgileri</param>
        /// <returns>Güncellenmiş geri bildirim</returns>
        [HttpPost("update/{id}/status")]
        [MenuAuthorize("UserFeedback", MenuPermission.Edit)]
        [ProducesResponseType(typeof(ResponseModel<UserFeedbackDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ResponseModel<UserFeedbackDto>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateFeedbackStatus(
            long id,
            [FromBody] UpdateFeedbackStatusDto dto)
        {
            var result = await _feedbackService.UpdateFeedbackStatusAsync(id, dto);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Geri bildirimi siler (soft delete)
        /// </summary>
        /// <param name="id">Geri bildirim ID</param>
        /// <returns>Başarı durumu</returns>
        [HttpPost("delete/{id}")]
        [MenuAuthorize("UserFeedback", MenuPermission.Edit)]
        [ProducesResponseType(typeof(ResponseModel<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ResponseModel<bool>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteFeedback(long id)
        {
            var result = await _feedbackService.DeleteFeedbackAsync(id);
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Geri bildirim istatistiklerini getirir
        /// </summary>
        /// <returns>İstatistik verileri</returns>
        [HttpGet("statistics")]
        [MenuAuthorize("UserFeedbackStatistics", MenuPermission.View)]
        [ProducesResponseType(typeof(ResponseModel<FeedbackStatisticsDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetStatistics()
        {
            var result = await _feedbackService.GetStatisticsAsync();
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Kullanıcının kendi geri bildirimlerini getirir
        /// </summary>
        /// <returns>Kullanıcının geri bildirimleri</returns>
        [HttpGet("my-feedbacks")]
        [MenuAuthorize("UserFeedback", MenuPermission.View)]
        [ProducesResponseType(typeof(ResponseModel<List<UserFeedbackDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMyFeedbacks()
        {
            var result = await _feedbackService.GetMyFeedbacksAsync();
            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Geri bildirime bir veya birden fazla dosya ekler.
        /// </summary>
        [HttpPost("{feedbackId:long}/attachments/create")]
        [MenuAuthorize("UserFeedback", MenuPermission.Edit)]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(525_000_000)]
        [RequestFormLimits(MultipartBodyLengthLimit = 525_000_000, ValueCountLimit = 32)]
        [ProducesResponseType(typeof(ResponseModel<List<UserFeedbackAttachmentDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> AddAttachments(
            [FromRoute] long feedbackId,
            [FromForm] List<IFormFile> files,
            CancellationToken cancellationToken)
        {
            var result = await _feedbackService.AddAttachmentsAsync(
                feedbackId,
                files,
                cancellationToken);

            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Geri bildirime ait aktif dosyaları getirir.
        /// </summary>
        [HttpGet("{feedbackId:long}/attachments")]
        [MenuAuthorize("UserFeedback", MenuPermission.View)]
        [ProducesResponseType(typeof(ResponseModel<List<UserFeedbackAttachmentDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAttachments(
            [FromRoute] long feedbackId,
            CancellationToken cancellationToken)
        {
            var result = await _feedbackService.GetAttachmentsAsync(
                feedbackId,
                cancellationToken);

            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Yetki kontrolünden sonra dosyanın normalize edilmiş indirme bilgisini döndürür.
        /// </summary>
        [HttpGet("attachments/{attachmentId:long}/download")]
        [MenuAuthorize("UserFeedback", MenuPermission.View)]
        [ProducesResponseType(typeof(ResponseModel<UserFeedbackAttachmentDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAttachmentDownload(
            [FromRoute] long attachmentId,
            CancellationToken cancellationToken)
        {
            var result = await _feedbackService.GetAttachmentDownloadAsync(
                attachmentId,
                cancellationToken);

            return StatusCode((int)result.StatusCode, result);
        }

        /// <summary>
        /// Geri bildirim dosyasını soft-delete eder ve storage nesnesini kaldırır.
        /// </summary>
        [HttpPost("attachments/delete/{attachmentId:long}")]
        [MenuAuthorize("UserFeedback", MenuPermission.Edit)]
        [ProducesResponseType(typeof(ResponseModel<bool>), StatusCodes.Status200OK)]
        public async Task<IActionResult> DeleteAttachment(
            [FromRoute] long attachmentId,
            CancellationToken cancellationToken)
        {
            var result = await _feedbackService.DeleteAttachmentAsync(
                attachmentId,
                cancellationToken);

            return StatusCode((int)result.StatusCode, result);
        }
    }
}
