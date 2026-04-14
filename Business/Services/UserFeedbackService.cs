using Business.Interfaces;
using Business.UnitOfWork;
using Core.Common;
using Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Model.Concrete;
using Model.Dtos.UserFeedbackDtos;
using System.Text.Json;

namespace Business.Services
{
    public class UserFeedbackService : IUserFeedbackService
    {
        private readonly IUnitOfWork _uow;
        private readonly ILogger<UserFeedbackService> _logger;
        private readonly ICurrentUser _currentUser;

        public UserFeedbackService(
            IUnitOfWork uow,
            ILogger<UserFeedbackService> logger,
            ICurrentUser currentUser)
        {
            _uow = uow;
            _logger = logger;
            _currentUser = currentUser;
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
                    Priority = 3, // Varsayılan orta öncelik
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

                // Admin değilse sadece kendi kaydını güncelleyebilir
                if (!isAdmin && feedback.CreatedUser != userId)
                {
                    return ResponseModel<UserFeedbackDto>.Fail(
                        "Bu geri bildirime erişim yetkiniz yok",
                        StatusCode.Unauthorized);
                }

                feedback.Status = dto.Status;
                feedback.UpdatedDate = DateTimeOffset.Now;
                feedback.UpdatedUser = userId;

                if (!string.IsNullOrWhiteSpace(dto.AdminResponse))
                {
                    feedback.AdminResponse = dto.AdminResponse;
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
                    "Geri bildirim durumu güncellendi. ID: {Id}, Yeni Durum: {Status}",
                    id,
                    dto.Status);

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

        #endregion
    }
}