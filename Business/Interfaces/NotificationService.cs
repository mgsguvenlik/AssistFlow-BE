using Business.UnitOfWork;
using Core.Common;
using Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Model.Concrete;
using Model.Dtos.Notification;
using Model.Dtos.Role;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.Interfaces
{
    public class NotificationService : INotificationService
    {
        private readonly IUnitOfWork _uow;
        private readonly ICurrentUser _currentUser;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(IUnitOfWork uow, ICurrentUser currentUser, ILogger<NotificationService> logger)
        {
            _uow = uow;
            _currentUser = currentUser;
            _logger = logger;
        }

        // ------------------------ Create (fan-out) ------------------------
        public async Task<ResponseModel> CreateAsync(NotificationCreateDto dto)
        {
            // hedef çözümlenmemişse hata
            bool any =
                (dto.TargetUserId.HasValue) ||
                (dto.TargetUserIds is { Count: > 0 }) ||
                (!string.IsNullOrWhiteSpace(dto.TargetRoleCode)) ||
                (dto.TargetRoleCodes is { Count: > 0 });

            if (!any) return ResponseModel.Fail("Hedef (kullanıcı/rol) belirtilmeli.", StatusCode.BadRequest);

            var payloadJson = dto.Payload is null ? null : System.Text.Json.JsonSerializer.Serialize(dto.Payload);

            // tek kullanıcı
            if (dto.TargetUserId.HasValue)
                await InsertAsync(NotificationScope.User, dto.TargetUserId.Value, null);

            // çoklu kullanıcı
            if (dto.TargetUserIds is { Count: > 0 })
                foreach (var uid in dto.TargetUserIds.Distinct())
                    await InsertAsync(NotificationScope.User, uid, null);

            // tek rol
            if (!string.IsNullOrWhiteSpace(dto.TargetRoleCode))
                await InsertAsync(NotificationScope.Role, null, dto.TargetRoleCode!.Trim());

            // çoklu rol
            if (dto.TargetRoleCodes is { Count: > 0 })
                foreach (var role in dto.TargetRoleCodes.Where(s => !string.IsNullOrWhiteSpace(s))
                                                        .Select(s => s.Trim())
                                                        .Distinct(StringComparer.OrdinalIgnoreCase))
                    await InsertAsync(NotificationScope.Role, null, role);

            await _uow.Repository.CompleteAsync();
            return ResponseModel.Success();

            // local helper
            async Task InsertAsync(NotificationScope scope, long? userId, string? roleCode)
            {
                var me = await _currentUser.GetAsync();
                var n = new Notification
                {
                    Type = dto.Type,
                    Scope = scope,
                    TargetUserId = scope == NotificationScope.User ? userId : null,
                    TargetRoleCode = scope == NotificationScope.Role ? roleCode : null,

                    Title = dto.Title,
                    Message = dto.Message,

                    RequestNo = dto.RequestNo,
                    FromStepCode = dto.FromStepCode,
                    ToStepCode = dto.ToStepCode,
                    ReviewNotes = dto.ReviewNotes,
                    PayloadJson = payloadJson,

                    IsRead = false,
                    CreatedDate = DateTime.Now,
                    CreatedUser = me?.Id
                };
                await _uow.Repository.AddAsync(n);
            }
        }

        public async Task<ResponseModel> CreateForUserAsync(NotificationCreateDto dto, long userId)
        {
            dto.TargetUserId = userId;
            dto.TargetUserIds = null;
            dto.TargetRoleCode = null;
            dto.TargetRoleCodes = null;
            return await CreateAsync(dto);
        }

        public async Task<ResponseModel> CreateForUsersAsync(NotificationCreateDto dto, IEnumerable<long> userIds)
        {
            dto.TargetUserId = null;
            dto.TargetUserIds = userIds?.Distinct().ToList();
            dto.TargetRoleCode = null;
            dto.TargetRoleCodes = null;
            return await CreateAsync(dto);
        }

        public async Task<ResponseModel> CreateForRoleAsync(NotificationCreateDto dto, string roleCode)
        {
            dto.TargetUserId = null;
            dto.TargetUserIds = null;
            dto.TargetRoleCode = roleCode;
            dto.TargetRoleCodes = null;
            return await CreateAsync(dto);
        }

        public async Task<ResponseModel> CreateForRolesAsync(NotificationCreateDto dto, IEnumerable<string> roleCodes)
        {
            dto.TargetUserId = null;
            dto.TargetUserIds = null;
            dto.TargetRoleCode = null;
            dto.TargetRoleCodes = roleCodes?.Where(x => !string.IsNullOrWhiteSpace(x))
                                            .Select(x => x.Trim())
                                            .Distinct(StringComparer.OrdinalIgnoreCase)
                                            .ToList();
            return await CreateAsync(dto);
        }

        // ------------------------ Query (me.Id ∪ roles) ------------------------
        public async Task<ResponseModel<PagedResult<NotificationGetDto>>> GetMyAsync(QueryParams q)
            {
            var me = await _currentUser.GetAsync();
            if (me is null) return ResponseModel<PagedResult<NotificationGetDto>>.Fail("Kullanıcı bulunamadı.", StatusCode.Unauthorized);

            var myId = me.Id;
            var myRoles = (me.Roles ?? new List<RoleGetDto>()).Select(r => r.Code).Where(s => !string.IsNullOrWhiteSpace(s))
                           .Select(s => s!.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var query = _uow.Repository.GetQueryable<Notification>().AsNoTracking()
                         .Where(n =>
                            (n.Scope == NotificationScope.User && n.TargetUserId == myId) ||
                            (n.Scope == NotificationScope.Role && n.TargetRoleCode != null && myRoles.Contains(n.TargetRoleCode)));

            if (!string.IsNullOrWhiteSpace(q.Search))
            {
                var term = q.Search.Trim();
                query = query.Where(n => n.Title.Contains(term) || n.Message.Contains(term) || (n.RequestNo ?? "").Contains(term));
            }

            var total = await query.CountAsync();

            var items = await query
                .OrderByDescending(n => n.CreatedDate)
                .Skip((q.Page - 1) * q.PageSize)
                .Take(q.PageSize)
                .Select(n => new NotificationGetDto
                {
                    Id = n.Id,
                    Type = n.Type,
                    Scope = n.Scope,
                    TargetUserId = n.TargetUserId,
                    TargetRoleCode = n.TargetRoleCode,
                    Title = n.Title,
                    Message = n.Message,
                    RequestNo = n.RequestNo,
                    FromStepCode = n.FromStepCode,
                    ToStepCode = n.ToStepCode,
                    ReviewNotes = n.ReviewNotes,
                    PayloadJson = n.PayloadJson,
                    IsRead = n.IsRead,
                    ReadAt = n.ReadAt,
                    CreatedDate = n.CreatedDate,
                    CreatedUser = n.CreatedUser
                })
                .ToListAsync();

            return ResponseModel<PagedResult<NotificationGetDto>>.Success(new PagedResult<NotificationGetDto>(items, total, q.Page, q.PageSize));
        }

        public async Task<ResponseModel<int>> CountMyUnreadAsync()
        {
            var me = await _currentUser.GetAsync();
            if (me is null) return ResponseModel<int>.Fail("Kullanıcı bulunamadı.", StatusCode.Unauthorized);

            var myId = me.Id;
            var myRoles = (me.Roles ?? new List<RoleGetDto>()).Select(r => r.Code).Where(s => !string.IsNullOrWhiteSpace(s))
                           .Select(s => s!.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var count = await _uow.Repository.GetQueryable<Notification>().AsNoTracking()
                .Where(n => !n.IsRead &&
                       ((n.Scope == NotificationScope.User && n.TargetUserId == myId) ||
                        (n.Scope == NotificationScope.Role && n.TargetRoleCode != null && myRoles.Contains(n.TargetRoleCode))))
                .CountAsync();

            return ResponseModel<int>.Success(count);
        }

        // ------------------------ State Changes ------------------------
        public async Task<ResponseModel> MarkAsReadAsync(long id)
        {
            var me = await _currentUser.GetAsync();
            if (me is null) return ResponseModel.Fail("Kullanıcı bulunamadı.", StatusCode.Unauthorized);

            var myId = me.Id;
            var myRoles = (me.Roles ?? new List<RoleGetDto>()).Select(r => r.Code).Where(s => !string.IsNullOrWhiteSpace(s))
                           .Select(s => s!.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);

            // sahiplik kontrolü
            var n = await _uow.Repository.GetQueryable<Notification>()
                        .FirstOrDefaultAsync(x => x.Id == id &&
                            ((x.Scope == NotificationScope.User && x.TargetUserId == myId) ||
                             (x.Scope == NotificationScope.Role && x.TargetRoleCode != null && myRoles.Contains(x.TargetRoleCode))));

            if (n is null) return ResponseModel.Fail("Kayıt bulunamadı ya da yetkiniz yok.", StatusCode.NotFound);

            if (!n.IsRead)
            {
                n.IsRead = true;
                n.ReadAt = DateTime.Now;
                _uow.Repository.Update(n);
                await _uow.Repository.CompleteAsync();
            }
            return ResponseModel.Success();
        }

        public async Task<ResponseModel> MarkAllMyAsReadAsync()
        {
            var me = await _currentUser.GetAsync();
            if (me is null) return ResponseModel.Fail("Kullanıcı bulunamadı.", StatusCode.Unauthorized);

            var myId = me.Id;
            var myRoles = (me.Roles ?? new List<RoleGetDto>()).Select(r => r.Code).Where(s => !string.IsNullOrWhiteSpace(s))
                           .Select(s => s!.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);

            // toplu update (EF Core’da tek tek de gidebilir; burada basitçe fetch + loop)
            var list = await _uow.Repository.GetQueryable<Notification>()
                .Where(n => !n.IsRead &&
                    ((n.Scope == NotificationScope.User && n.TargetUserId == myId) ||
                     (n.Scope == NotificationScope.Role && n.TargetRoleCode != null && myRoles.Contains(n.TargetRoleCode))))
                .ToListAsync();

            var now = DateTime.Now;
            foreach (var n in list)
            {
                n.IsRead = true;
                n.ReadAt = now;
                _uow.Repository.Update(n);
            }
            await _uow.Repository.CompleteAsync();
            return ResponseModel.Success();
        }
    }
}
