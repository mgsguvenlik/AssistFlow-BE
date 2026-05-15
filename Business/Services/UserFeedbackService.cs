using Business.Interfaces;
using Business.UnitOfWork;
using Core.Common;
using Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Model.Concrete;
using Model.Dtos.UserFeedbackDtos;
using System.Net;
using System.Text.Json;

namespace Business.Services
{
    public class UserFeedbackService : IUserFeedbackService
    {
        private readonly IUnitOfWork _uow;
        private readonly ILogger<UserFeedbackService> _logger;
        private readonly ICurrentUser _currentUser;
        private readonly IMailPushService _mailPushService;

        public UserFeedbackService(
            IUnitOfWork uow,
            ILogger<UserFeedbackService> logger,
            ICurrentUser currentUser,
            IMailPushService mailPushService)
        {
            _uow = uow;
            _logger = logger;
            _currentUser = currentUser;
            _mailPushService = mailPushService;
        }

        public async Task<ResponseModel<UserFeedbackDto>> CreateFeedbackAsync(
      CreateUserFeedbackDto dto,
      string? userAgent = null)
        {
            try
            {
                var me = await _currentUser.GetAsync();
                var userId = me?.Id ?? 0;

                if (userId <= 0)
                {
                    return ResponseModel<UserFeedbackDto>.Fail(
                        "Kullanıcı bilgisi bulunamadı",
                        StatusCode.Unauthorized);
                }

                var feedback = new UserFeedback
                {
                    Title = dto.Title,
                    Description = dto.Description,
                    FeedbackType = dto.FeedbackType,
                    Status = FeedbackStatus.Created,
                    Priority = 3,
                    RelatedUrl = dto.RelatedUrl,
                    UserAgent = userAgent,
                    AttachmentUrls = dto.AttachmentUrls != null && dto.AttachmentUrls.Any()
                        ? JsonSerializer.Serialize(dto.AttachmentUrls)
                        : null,
                    CreatedDate = DateTimeOffset.Now,
                    CreatedUser = userId,
                    IsDeleted = false
                };

                await _uow.Repository.AddAsync(feedback);
                await _uow.Repository.CompleteAsync();

                _logger.LogInformation(
                    "Yeni geri bildirim oluşturuldu. ID: {Id}, Tip: {Type}, Kullanıcı: {UserId}",
                    feedback.Id,
                    feedback.FeedbackType,
                    userId);

                // Yeni feedback oluşturulduğunda ilgili yönetici mail hesaplarına bildirim kuyruğu oluşturulur.
                await EnqueueNewFeedbackCreatedMailAsync(feedback);

                return ResponseModel<UserFeedbackDto>.Success(await MapToDto(feedback));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CreateFeedbackAsync hatası");
                return ResponseModel<UserFeedbackDto>.Fail(
                    $"Geri bildirim oluşturulurken hata: {ex.Message}",
                    StatusCode.Error);
            }
        }

        public async Task<ResponseModel<PaginatedList<UserFeedbackDto>>> GetFeedbacksAsync(
            int page = 1,
            int pageSize = 20,
            string? search = null,
            FeedbackStatus? status = null,
            FeedbackType? type = null)
        {
            try
            {
                var me = await _currentUser.GetAsync();
                var userId = me?.Id ?? 0;

                if (userId <= 0)
                {
                    return ResponseModel<PaginatedList<UserFeedbackDto>>.Fail(
                        "Kullanıcı bilgisi bulunamadı",
                        StatusCode.Unauthorized);
                }

                var isAdmin = IsUserAdmin(me);

                var query = _uow.Repository
                    .GetQueryable<UserFeedback>()
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted);

                // Admin değilse sadece kendi kayıtlarını görebilir
                if (!isAdmin)
                {
                    query = query.Where(x => x.CreatedUser == userId);
                }

                // Filtreleme
                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(x =>
                        x.Title.Contains(search) ||
                        x.Description.Contains(search));
                }

                if (status.HasValue)
                {
                    query = query.Where(x => x.Status == status.Value);
                }

                if (type.HasValue)
                {
                    query = query.Where(x => x.FeedbackType == type.Value);
                }

                // Sıralama: Yeni oluşturulanlar ve yüksek öncelikli ilk sırada
                query = query.OrderByDescending(x => x.CreatedDate);

                var totalCount = await query.CountAsync();
                var items = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var dtos = new List<UserFeedbackDto>();
                foreach (var item in items)
                {
                    dtos.Add(await MapToDto(item));
                }

                var paginatedList = new PaginatedList<UserFeedbackDto>(
                    dtos,
                    totalCount,
                    page,
                    pageSize);

                return ResponseModel<PaginatedList<UserFeedbackDto>>.Success(paginatedList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetFeedbacksAsync hatası");
                return ResponseModel<PaginatedList<UserFeedbackDto>>.Fail(
                    $"Geri bildirimler getirilirken hata: {ex.Message}",
                    StatusCode.Error);
            }
        }

        public async Task<ResponseModel<UserFeedbackDto>> GetFeedbackByIdAsync(long id)
        {
            try
            {
                var me = await _currentUser.GetAsync();
                var userId = me?.Id ?? 0;

                if (userId <= 0)
                {
                    return ResponseModel<UserFeedbackDto>.Fail(
                        "Kullanıcı bilgisi bulunamadı",
                        StatusCode.Unauthorized);
                }

                var isAdmin = IsUserAdmin(me);

                var feedback = await _uow.Repository
                    .GetQueryable<UserFeedback>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

                if (feedback == null)
                {
                    return ResponseModel<UserFeedbackDto>.Fail(
                        "Geri bildirim bulunamadı",
                        StatusCode.NotFound);
                }

                // Admin değilse sadece kendi kaydını görebilir
                if (!isAdmin && feedback.CreatedUser != userId)
                {
                    return ResponseModel<UserFeedbackDto>.Fail(
                        "Bu geri bildirime erişim yetkiniz yok",
                        StatusCode.Unauthorized);
                }

                return ResponseModel<UserFeedbackDto>.Success(await MapToDto(feedback));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetFeedbackByIdAsync hatası. ID: {Id}", id);
                return ResponseModel<UserFeedbackDto>.Fail(
                    $"Geri bildirim getirilirken hata: {ex.Message}",
                    StatusCode.Error);
            }
        }


        public async Task<ResponseModel<bool>> DeleteFeedbackAsync(long id)
        {
            try
            {
                var me = await _currentUser.GetAsync();
                var userId = me?.Id ?? 0;

                if (userId <= 0)
                {
                    return ResponseModel<bool>.Fail(
                        "Kullanıcı bilgisi bulunamadı",
                        StatusCode.Unauthorized);
                }

                var isAdmin = IsUserAdmin(me);

                var feedback = await _uow.Repository
                    .GetQueryable<UserFeedback>()
                    .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

                if (feedback == null)
                {
                    return ResponseModel<bool>.Fail(
                        "Geri bildirim bulunamadı",
                        StatusCode.NotFound);
                }

                // Admin değilse sadece kendi kaydını silebilir
                if (!isAdmin && feedback.CreatedUser != userId)
                {
                    return ResponseModel<bool>.Fail(
                        "Bu geri bildirime erişim yetkiniz yok",
                        StatusCode.Unauthorized);
                }

                feedback.IsDeleted = true;
                feedback.UpdatedDate = DateTimeOffset.Now;
                feedback.UpdatedUser = userId;

                _uow.Repository.Update(feedback);
                await _uow.Repository.CompleteAsync();

                _logger.LogInformation("Geri bildirim silindi. ID: {Id}", id);

                return ResponseModel<bool>.Success(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DeleteFeedbackAsync hatası. ID: {Id}", id);
                return ResponseModel<bool>.Fail(
                    $"Geri bildirim silinirken hata: {ex.Message}",
                    StatusCode.Error);
            }
        }

        public async Task<ResponseModel<FeedbackStatisticsDto>> GetStatisticsAsync()
        {
            try
            {
                var me = await _currentUser.GetAsync();
                var userId = me?.Id ?? 0;

                if (userId <= 0)
                {
                    return ResponseModel<FeedbackStatisticsDto>.Fail(
                        "Kullanıcı bilgisi bulunamadı",
                        StatusCode.Unauthorized);
                }

                var isAdmin = IsUserAdmin(me);

                var query = _uow.Repository
                    .GetQueryable<UserFeedback>()
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted);

                // Admin değilse sadece kendi kayıtlarının istatistiklerini görebilir
                if (!isAdmin)
                {
                    query = query.Where(x => x.CreatedUser == userId);
                }

                var feedbacks = await query.ToListAsync();

                var totalCount = feedbacks.Count;

                // Durum bazlı sayımlar
                var createdCount = feedbacks.Count(x => x.Status == FeedbackStatus.Created);
                var underReviewCount = feedbacks.Count(x => x.Status == FeedbackStatus.UnderReview);
                var inProgressCount = feedbacks.Count(x => x.Status == FeedbackStatus.InProgress);
                var completedCount = feedbacks.Count(x => x.Status == FeedbackStatus.Completed);
                var rejectedCount = feedbacks.Count(x => x.Status == FeedbackStatus.Rejected);
                var closedCount = feedbacks.Count(x => x.Status == FeedbackStatus.Closed);

                // Tip bazlı sayımlar
                var suggestionCount = feedbacks.Count(x => x.FeedbackType == FeedbackType.Suggestion);
                var featureRequestCount = feedbacks.Count(x => x.FeedbackType == FeedbackType.FeatureRequest);
                var bugReportCount = feedbacks.Count(x => x.FeedbackType == FeedbackType.BugReport);
                var issueCount = feedbacks.Count(x => x.FeedbackType == FeedbackType.Issue);
                var improvementCount = feedbacks.Count(x => x.FeedbackType == FeedbackType.Improvement);

                // Ortalama yanıt süresi
                var respondedFeedbacks = feedbacks
                    .Where(x => x.ResponseDate.HasValue)
                    .ToList();

                var avgResponseTimeHours = respondedFeedbacks.Any()
                    ? respondedFeedbacks
                        .Select(x => (x.ResponseDate!.Value - x.CreatedDate).TotalHours)
                        .Average()
                    : 0;

                // Tamamlanma oranı
                var completionRate = totalCount > 0
                    ? (double)completedCount / totalCount * 100
                    : 0;

                var dto = new FeedbackStatisticsDto
                {
                    TotalFeedbacks = totalCount,
                    CreatedCount = createdCount,
                    UnderReviewCount = underReviewCount,
                    InProgressCount = inProgressCount,
                    CompletedCount = completedCount,
                    RejectedCount = rejectedCount,
                    ClosedCount = closedCount,
                    SuggestionCount = suggestionCount,
                    FeatureRequestCount = featureRequestCount,
                    BugReportCount = bugReportCount,
                    IssueCount = issueCount,
                    ImprovementCount = improvementCount,
                    AverageResponseTimeHours = Math.Round(avgResponseTimeHours, 2),
                    CompletionRate = Math.Round(completionRate, 2)
                };

                return ResponseModel<FeedbackStatisticsDto>.Success(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetStatisticsAsync hatası");
                return ResponseModel<FeedbackStatisticsDto>.Fail(
                    $"İstatistikler getirilirken hata: {ex.Message}",
                    StatusCode.Error);
            }
        }

        public async Task<ResponseModel<List<UserFeedbackDto>>> GetMyFeedbacksAsync()
        {
            try
            {
                var me = await _currentUser.GetAsync();
                var userId = me?.Id ?? 0;

                if (userId <= 0)
                {
                    return ResponseModel<List<UserFeedbackDto>>.Fail(
                        "Kullanıcı bilgisi bulunamadı",
                        StatusCode.Unauthorized);
                }

                var feedbacks = await _uow.Repository
                    .GetQueryable<UserFeedback>()
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted && x.CreatedUser == userId)
                    .OrderByDescending(x => x.CreatedDate)
                    .ToListAsync();

                var dtos = new List<UserFeedbackDto>();
                foreach (var feedback in feedbacks)
                {
                    dtos.Add(await MapToDto(feedback));
                }

                return ResponseModel<List<UserFeedbackDto>>.Success(dtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetMyFeedbacksAsync hatası");
                return ResponseModel<List<UserFeedbackDto>>.Fail(
                    $"Kendi geri bildirimleriniz getirilirken hata: {ex.Message}",
                    StatusCode.Error);
            }
        }

        #region Private Methods

        /// <summary>
        /// Kullanıcının ADMIN rolünde olup olmadığını kontrol eder
        /// </summary>
        private static bool IsUserAdmin(Model.Dtos.Auth.CurrentUserDto? user)
        {
            if (user == null || user.Roles == null || !user.Roles.Any())
                return false;

            return user.Roles.Any(r =>
                r.Code?.Equals("ADMIN", StringComparison.OrdinalIgnoreCase) == true ||
                r.Name?.Equals("ADMIN", StringComparison.OrdinalIgnoreCase) == true);
        }

        private async Task<UserFeedbackDto> MapToDto(UserFeedback feedback)
        {
            // Kullanıcı bilgilerini getir
            var createdUser = await _uow.Repository
                .GetQueryable<User>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == feedback.CreatedUser);

            User? respondedUser = null;
            if (feedback.RespondedBy.HasValue)
            {
                respondedUser = await _uow.Repository
                    .GetQueryable<User>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == feedback.RespondedBy.Value);
            }

            return new UserFeedbackDto
            {
                Id = feedback.Id,
                Title = feedback.Title,
                Description = feedback.Description,
                FeedbackType = feedback.FeedbackType,
                FeedbackTypeText = GetFeedbackTypeText(feedback.FeedbackType),
                Status = feedback.Status,
                StatusText = GetStatusText(feedback.Status),
                Priority = feedback.Priority,
                AdminResponse = feedback.AdminResponse,
                ResponseDate = feedback.ResponseDate,
                RespondedBy = feedback.RespondedBy,
                RespondedByName = respondedUser?.TechnicianName,
                CompletedDate = feedback.CompletedDate,
                RelatedUrl = feedback.RelatedUrl,
                UserAgent = feedback.UserAgent,
                AttachmentUrls = !string.IsNullOrWhiteSpace(feedback.AttachmentUrls)
                    ? JsonSerializer.Deserialize<List<string>>(feedback.AttachmentUrls)
                    : null,
                CreatedUser = feedback.CreatedUser,
                CreatedUserName = createdUser?.TechnicianName,
                CreatedDate = feedback.CreatedDate,
                UpdatedDate = feedback.UpdatedDate
            };
        }

        private static string GetFeedbackTypeText(FeedbackType type)
        {
            return type switch
            {
                FeedbackType.Suggestion => "Öneri",
                FeedbackType.FeatureRequest => "Özellik Talebi",
                FeedbackType.BugReport => "Hata Bildirimi",
                FeedbackType.Issue => "Sorun",
                FeedbackType.Improvement => "İyileştirme",
                FeedbackType.Other => "Diğer",
                _ => "Bilinmiyor"
            };
        }

        private static string GetStatusText(FeedbackStatus status)
        {
            return status switch
            {
                FeedbackStatus.Created => "Oluşturuldu",
                FeedbackStatus.UnderReview => "İnceleniyor",
                FeedbackStatus.InProgress => "Devam Ediyor",
                FeedbackStatus.Completed => "Tamamlandı",
                FeedbackStatus.Rejected => "Reddedildi",
                FeedbackStatus.Closed => "Kapatıldı",
                _ => "Bilinmiyor"
            };
        }


        public async Task<ResponseModel<UserFeedbackDto>> UpdateFeedbackStatusAsync(
    long id,
    UpdateFeedbackStatusDto dto)
        {
            try
            {
                var me = await _currentUser.GetAsync();
                var userId = me?.Id ?? 0;

                if (userId <= 0)
                {
                    return ResponseModel<UserFeedbackDto>.Fail(
                        "Kullanıcı bilgisi bulunamadı",
                        StatusCode.Unauthorized);
                }

                var isAdmin = IsUserAdmin(me);

                var feedback = await _uow.Repository
                    .GetQueryable<UserFeedback>()
                    .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

                if (feedback == null)
                {
                    return ResponseModel<UserFeedbackDto>.Fail(
                        "Geri bildirim bulunamadı",
                        StatusCode.NotFound);
                }

                if (!isAdmin && feedback.CreatedUser != userId)
                {
                    return ResponseModel<UserFeedbackDto>.Fail(
                        "Bu geri bildirime erişim yetkiniz yok",
                        StatusCode.Unauthorized);
                }

                var oldStatus = feedback.Status;
                var oldAdminResponse = feedback.AdminResponse;

                var statusChanged = oldStatus != dto.Status;

                var hasNewAdminResponse =
                    !string.IsNullOrWhiteSpace(dto.AdminResponse) &&
                    !string.Equals(
                        oldAdminResponse?.Trim(),
                        dto.AdminResponse.Trim(),
                        StringComparison.Ordinal);

                feedback.Status = dto.Status;
                feedback.UpdatedDate = DateTimeOffset.Now;
                feedback.UpdatedUser = userId;

                if (hasNewAdminResponse)
                {
                    feedback.AdminResponse = dto.AdminResponse.Trim();
                    feedback.ResponseDate = DateTimeOffset.Now;
                    feedback.RespondedBy = userId;
                }

                if (dto.Priority.HasValue)
                {
                    feedback.Priority = dto.Priority.Value;
                }

                if (dto.Status == FeedbackStatus.Completed || dto.Status == FeedbackStatus.Closed)
                {
                    feedback.CompletedDate = DateTimeOffset.Now;
                }

                _uow.Repository.Update(feedback);
                await _uow.Repository.CompleteAsync();

                _logger.LogInformation(
                    "Geri bildirim güncellendi. ID: {Id}, Yeni Durum: {Status}",
                    id,
                    dto.Status);

                // Durum eski değerinden farklıysa feedback sahibine mail kuyruğu oluşturulur.
                if (statusChanged)
                {
                    await EnqueueFeedbackStatusChangedMailAsync(feedback, oldStatus, dto.Status);
                }

                // AdminResponse eski değerinden farklı ve doluysa feedback sahibine mail kuyruğu oluşturulur.
                if (hasNewAdminResponse)
                {
                    await EnqueueFeedbackAdminResponseMailAsync(feedback);
                }

                return ResponseModel<UserFeedbackDto>.Success(await MapToDto(feedback));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdateFeedbackStatusAsync hatası. ID: {Id}", id);
                return ResponseModel<UserFeedbackDto>.Fail(
                    $"Geri bildirim güncellenirken hata: {ex.Message}",
                    StatusCode.Error);
            }
        }


        private static string BuildNewFeedbackCreatedMailBody(UserFeedback feedback, User? createdUser)
        {
            var title = WebUtility.HtmlEncode(feedback.Title);
            var description = WebUtility.HtmlEncode(feedback.Description);
            var createdUserName = WebUtility.HtmlEncode(createdUser?.TechnicianName ?? "Bilinmiyor");
            var createdUserEmail = WebUtility.HtmlEncode(createdUser?.TechnicianEmail ?? "-");
            var feedbackType = WebUtility.HtmlEncode(GetFeedbackTypeText(feedback.FeedbackType));
            var createdDate = feedback.CreatedDate.ToString("dd.MM.yyyy HH:mm");

            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8' />
</head>
<body style='margin:0;padding:0;background-color:#f4f6f8;font-family:Arial,Helvetica,sans-serif;color:#1f2937;'>
    <table width='100%' cellpadding='0' cellspacing='0' style='background-color:#f4f6f8;padding:24px 0;'>
        <tr>
            <td align='center'>
                <table width='640' cellpadding='0' cellspacing='0' style='background-color:#ffffff;border-radius:12px;overflow:hidden;border:1px solid #e5e7eb;'>
                    <tr>
                        <td style='background-color:#1f4e79;color:#ffffff;padding:20px 24px;'>
                            <h2 style='margin:0;font-size:20px;'>Yeni Geri Bildirim Talebi Oluşturuldu</h2>
                        </td>
                    </tr>
                    <tr>
                        <td style='padding:24px;'>
                            <p style='font-size:15px;line-height:1.6;margin:0 0 16px;'>
                                Merhaba,
                            </p>

                            <p style='font-size:15px;line-height:1.6;margin:0 0 20px;'>
                                Sistemde yeni bir geri bildirim talebi oluşturulmuştur. Talep detayları aşağıdadır.
                            </p>

                            <table width='100%' cellpadding='0' cellspacing='0' style='border-collapse:collapse;margin-top:12px;'>
                                <tr>
                                    <td style='padding:10px;border:1px solid #e5e7eb;background:#f9fafb;width:180px;'><strong>Talep No</strong></td>
                                    <td style='padding:10px;border:1px solid #e5e7eb;'>#{feedback.Id}</td>
                                </tr>
                                <tr>
                                    <td style='padding:10px;border:1px solid #e5e7eb;background:#f9fafb;'><strong>Başlık</strong></td>
                                    <td style='padding:10px;border:1px solid #e5e7eb;'>{title}</td>
                                </tr>
                                <tr>
                                    <td style='padding:10px;border:1px solid #e5e7eb;background:#f9fafb;'><strong>Tip</strong></td>
                                    <td style='padding:10px;border:1px solid #e5e7eb;'>{feedbackType}</td>
                                </tr>
                                <tr>
                                    <td style='padding:10px;border:1px solid #e5e7eb;background:#f9fafb;'><strong>Oluşturan Kullanıcı</strong></td>
                                    <td style='padding:10px;border:1px solid #e5e7eb;'>{createdUserName}</td>
                                </tr>
                                <tr>
                                    <td style='padding:10px;border:1px solid #e5e7eb;background:#f9fafb;'><strong>E-posta</strong></td>
                                    <td style='padding:10px;border:1px solid #e5e7eb;'>{createdUserEmail}</td>
                                </tr>
                                <tr>
                                    <td style='padding:10px;border:1px solid #e5e7eb;background:#f9fafb;'><strong>Oluşturma Tarihi</strong></td>
                                    <td style='padding:10px;border:1px solid #e5e7eb;'>{createdDate}</td>
                                </tr>
                            </table>

                            <div style='margin-top:20px;padding:14px;background:#f9fafb;border:1px solid #e5e7eb;border-radius:8px;'>
                                <strong>Açıklama:</strong>
                                <p style='margin:8px 0 0;line-height:1.6;'>{description}</p>
                            </div>

                            <p style='font-size:14px;line-height:1.6;margin:24px 0 0;color:#6b7280;'>
                                Bu bildirim FlowAssist sistemi tarafından otomatik olarak oluşturulmuştur.
                            </p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
        }

        private static string BuildFeedbackStatusChangedMailBody(
            UserFeedback feedback,
            User owner,
            FeedbackStatus oldStatus,
            FeedbackStatus newStatus,
            string statusMessage)
        {
            var ownerName = WebUtility.HtmlEncode(owner.TechnicianName);
            var title = WebUtility.HtmlEncode(feedback.Title);
            var oldStatusText = WebUtility.HtmlEncode(GetStatusText(oldStatus));
            var newStatusText = WebUtility.HtmlEncode(GetStatusText(newStatus));
            var message = WebUtility.HtmlEncode(statusMessage);

            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8' />
</head>
<body style='margin:0;padding:0;background-color:#f4f6f8;font-family:Arial,Helvetica,sans-serif;color:#1f2937;'>
    <table width='100%' cellpadding='0' cellspacing='0' style='background-color:#f4f6f8;padding:24px 0;'>
        <tr>
            <td align='center'>
                <table width='640' cellpadding='0' cellspacing='0' style='background-color:#ffffff;border-radius:12px;overflow:hidden;border:1px solid #e5e7eb;'>
                    <tr>
                        <td style='background-color:#2563eb;color:#ffffff;padding:20px 24px;'>
                            <h2 style='margin:0;font-size:20px;'>Geri Bildirim Talebiniz Güncellendi</h2>
                        </td>
                    </tr>
                    <tr>
                        <td style='padding:24px;'>
                            <p style='font-size:15px;line-height:1.6;margin:0 0 16px;'>
                                Merhaba {ownerName},
                            </p>

                            <p style='font-size:15px;line-height:1.6;margin:0 0 20px;'>
                                <strong>#{feedback.Id}</strong> numaralı geri bildirim talebiniz <strong>{message}</strong>.
                            </p>

                            <table width='100%' cellpadding='0' cellspacing='0' style='border-collapse:collapse;margin-top:12px;'>
                                <tr>
                                    <td style='padding:10px;border:1px solid #e5e7eb;background:#f9fafb;width:180px;'><strong>Talep No</strong></td>
                                    <td style='padding:10px;border:1px solid #e5e7eb;'>#{feedback.Id}</td>
                                </tr>
                                <tr>
                                    <td style='padding:10px;border:1px solid #e5e7eb;background:#f9fafb;'><strong>Başlık</strong></td>
                                    <td style='padding:10px;border:1px solid #e5e7eb;'>{title}</td>
                                </tr>
                                <tr>
                                    <td style='padding:10px;border:1px solid #e5e7eb;background:#f9fafb;'><strong>Önceki Durum</strong></td>
                                    <td style='padding:10px;border:1px solid #e5e7eb;'>{oldStatusText}</td>
                                </tr>
                                <tr>
                                    <td style='padding:10px;border:1px solid #e5e7eb;background:#f9fafb;'><strong>Yeni Durum</strong></td>
                                    <td style='padding:10px;border:1px solid #e5e7eb;'>{newStatusText}</td>
                                </tr>
                            </table>

                            <div style='margin-top:24px;text-align:center;'>
                                <a href='https://flowassist.mgs.com.tr'
                                   style='display:inline-block;background:#2563eb;color:#ffffff;text-decoration:none;padding:12px 20px;border-radius:8px;font-weight:bold;'>
                                    Talebi Görüntüle
                                </a>
                            </div>

                            <p style='font-size:14px;line-height:1.6;margin:24px 0 0;color:#6b7280;'>
                                Detayları görüntülemek için FlowAssist sistemine giriş yapabilirsiniz.
                            </p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
        }

        private static string BuildFeedbackAdminResponseMailBody(UserFeedback feedback, User owner)
        {
            var ownerName = WebUtility.HtmlEncode(owner.TechnicianName);
            var title = WebUtility.HtmlEncode(feedback.Title);
            var adminResponse = WebUtility.HtmlEncode(feedback.AdminResponse ?? "");

            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8' />
</head>
<body style='margin:0;padding:0;background-color:#f4f6f8;font-family:Arial,Helvetica,sans-serif;color:#1f2937;'>
    <table width='100%' cellpadding='0' cellspacing='0' style='background-color:#f4f6f8;padding:24px 0;'>
        <tr>
            <td align='center'>
                <table width='640' cellpadding='0' cellspacing='0' style='background-color:#ffffff;border-radius:12px;overflow:hidden;border:1px solid #e5e7eb;'>
                    <tr>
                        <td style='background-color:#047857;color:#ffffff;padding:20px 24px;'>
                            <h2 style='margin:0;font-size:20px;'>Geri Bildirim Talebinize Açıklama Eklendi</h2>
                        </td>
                    </tr>
                    <tr>
                        <td style='padding:24px;'>
                            <p style='font-size:15px;line-height:1.6;margin:0 0 16px;'>
                                Merhaba {ownerName},
                            </p>

                            <p style='font-size:15px;line-height:1.6;margin:0 0 20px;'>
                                <strong>#{feedback.Id}</strong> numaralı geri bildirim talebiniz için bir açıklama eklenmiştir.
                            </p>

                            <table width='100%' cellpadding='0' cellspacing='0' style='border-collapse:collapse;margin-top:12px;'>
                                <tr>
                                    <td style='padding:10px;border:1px solid #e5e7eb;background:#f9fafb;width:180px;'><strong>Talep No</strong></td>
                                    <td style='padding:10px;border:1px solid #e5e7eb;'>#{feedback.Id}</td>
                                </tr>
                                <tr>
                                    <td style='padding:10px;border:1px solid #e5e7eb;background:#f9fafb;'><strong>Başlık</strong></td>
                                    <td style='padding:10px;border:1px solid #e5e7eb;'>{title}</td>
                                </tr>
                            </table>

                            <div style='margin-top:20px;padding:14px;background:#f9fafb;border:1px solid #e5e7eb;border-radius:8px;'>
                                <strong>Eklenen Açıklama:</strong>
                                <p style='margin:8px 0 0;line-height:1.6;'>{adminResponse}</p>
                            </div>

                            <div style='margin-top:24px;text-align:center;'>
                                <a href='https://flowassist.mgs.com.tr'
                                   style='display:inline-block;background:#047857;color:#ffffff;text-decoration:none;padding:12px 20px;border-radius:8px;font-weight:bold;'>
                                    Talebi Kontrol Et
                                </a>
                            </div>

                            <p style='font-size:14px;line-height:1.6;margin:24px 0 0;color:#6b7280;'>
                                Bu bilgilendirme FlowAssist sistemi tarafından otomatik olarak gönderilmiştir.
                            </p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
        }


        private async Task EnqueueNewFeedbackCreatedMailAsync(UserFeedback feedback)
        {
            try
            {
                var recipients = await GetFeedbackNotificationRecipientsAsync();

                if (string.IsNullOrWhiteSpace(recipients))
                {
                    _logger.LogWarning(
                        "Yeni feedback maili gönderilemedi. Configuration tablosunda FeedbackNotificationEmails değeri bulunamadı. FeedbackId: {FeedbackId}",
                        feedback.Id);

                    return;
                }

                var createdUser = await GetFeedbackOwnerAsync(feedback.CreatedUser);

                var subject = $"Yeni Geri Bildirim Talebi Oluşturuldu - Talep No: {feedback.Id}";

                var body = BuildNewFeedbackCreatedMailBody(feedback, createdUser);

                await _mailPushService.EnqueueAsync(new MailOutbox
                {
                    RequestNo = feedback.Id.ToString(),
                    FromStepCode = "FEEDBACK_CREATED",
                    ToStepCode = "ADMIN_NOTIFICATION",
                    ToRecipients = recipients,
                    Subject = subject,
                    BodyHtml = body,
                    Status = MailOutboxStatus.Pending,
                    TryCount = 0,
                    MaxTry = 5,
                    NextAttemptAt = DateTime.Now,
                    CreatedDate = DateTime.Now,
                    CreatedUser = feedback.CreatedUser
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Yeni feedback mail kuyruğu oluşturulamadı. FeedbackId: {FeedbackId}", feedback.Id);
            }
        }

        private async Task EnqueueFeedbackStatusChangedMailAsync(
            UserFeedback feedback,
            FeedbackStatus oldStatus,
            FeedbackStatus newStatus)
        {
            try
            {
                var owner = await GetFeedbackOwnerAsync(feedback.CreatedUser);

                if (owner == null || string.IsNullOrWhiteSpace(owner.TechnicianEmail))
                {
                    _logger.LogWarning(
                        "Feedback status maili gönderilemedi. Kullanıcı veya e-posta adresi bulunamadı. FeedbackId: {FeedbackId}, UserId: {UserId}",
                        feedback.Id,
                        feedback.CreatedUser);

                    return;
                }

                var statusMessage = GetStatusMailMessage(newStatus);
                var statusText = GetStatusText(newStatus);

                var subject = $"Geri Bildirim Talebiniz Güncellendi - Talep No: {feedback.Id}";

                var body = BuildFeedbackStatusChangedMailBody(
                    feedback,
                    owner,
                    oldStatus,
                    newStatus,
                    statusMessage);

                await _mailPushService.EnqueueAsync(new MailOutbox
                {
                    RequestNo = feedback.Id.ToString(),
                    FromStepCode = oldStatus.ToString(),
                    ToStepCode = newStatus.ToString(),
                    ToRecipients = owner.TechnicianEmail,
                    Subject = subject,
                    BodyHtml = body,
                    Status = MailOutboxStatus.Pending,
                    TryCount = 0,
                    MaxTry = 5,
                    NextAttemptAt = DateTime.Now,
                    CreatedDate = DateTime.Now,
                    CreatedUser = feedback.UpdatedUser
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Feedback status mail kuyruğu oluşturulamadı. FeedbackId: {FeedbackId}", feedback.Id);
            }
        }

        private async Task EnqueueFeedbackAdminResponseMailAsync(UserFeedback feedback)
        {
            try
            {
                var owner = await GetFeedbackOwnerAsync(feedback.CreatedUser);

                if (owner == null || string.IsNullOrWhiteSpace(owner.TechnicianEmail))
                {
                    _logger.LogWarning(
                        "Feedback admin response maili gönderilemedi. Kullanıcı veya e-posta adresi bulunamadı. FeedbackId: {FeedbackId}, UserId: {UserId}",
                        feedback.Id,
                        feedback.CreatedUser);

                    return;
                }

                var subject = $"Geri Bildirim Talebinize Açıklama Eklendi - Talep No: {feedback.Id}";

                var body = BuildFeedbackAdminResponseMailBody(feedback, owner);

                await _mailPushService.EnqueueAsync(new MailOutbox
                {
                    RequestNo = feedback.Id.ToString(),
                    FromStepCode = "ADMIN_RESPONSE_UPDATED",
                    ToStepCode = "USER_NOTIFICATION",
                    ToRecipients = owner.TechnicianEmail,
                    Subject = subject,
                    BodyHtml = body,
                    Status = MailOutboxStatus.Pending,
                    TryCount = 0,
                    MaxTry = 5,
                    NextAttemptAt = DateTime.Now,
                    CreatedDate = DateTime.Now,
                    CreatedUser = feedback.UpdatedUser
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Feedback admin response mail kuyruğu oluşturulamadı. FeedbackId: {FeedbackId}", feedback.Id);
            }
        }

        private async Task<string?> GetFeedbackNotificationRecipientsAsync()
        {
            var config = await _uow.Repository
                .GetQueryable<Configuration>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Name == "FeedbackNotificationEmails");

            if (config == null || string.IsNullOrWhiteSpace(config.Value))
                return null;

            var emails = config.Value
                .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();

            return emails.Any()
                ? string.Join(";", emails)
                : null;
        }

        private async Task<User?> GetFeedbackOwnerAsync(long? userId)
        {
            if (!userId.HasValue || userId.Value <= 0)
                return null;

            return await _uow.Repository
                .GetQueryable<User>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == userId.Value);
        }

        private static string GetStatusMailMessage(FeedbackStatus status)
        {
            return status switch
            {
                FeedbackStatus.UnderReview => "incelemeye alındı",
                FeedbackStatus.InProgress => "işleme alındı",
                FeedbackStatus.Completed => "tamamlandı",
                FeedbackStatus.Rejected => "reddedildi",
                FeedbackStatus.Closed => "kapatıldı",
                FeedbackStatus.Created => "oluşturuldu",
                _ => "güncellendi"
            };
        }

        #endregion
    }
}