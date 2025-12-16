using Core.Common;
using Model.Dtos.Notification;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.Interfaces
{
    public interface INotificationService
    {
        Task<ResponseModel> CreateAsync(NotificationCreateDto dto);                       // tek/çoklu hedefi içinden fan-out
        Task<ResponseModel> CreateForUserAsync(NotificationCreateDto dto, long userId);
        Task<ResponseModel> CreateForUsersAsync(NotificationCreateDto dto, IEnumerable<long> userIds);
        Task<ResponseModel> CreateForRoleAsync(NotificationCreateDto dto, string roleCode);
        Task<ResponseModel> CreateForRolesAsync(NotificationCreateDto dto, IEnumerable<string> roleCodes);

        Task<ResponseModel<PagedResult<NotificationGetDto>>> GetMyAsync(QueryParams q);   // (me.Id ∪ roles)
        Task<ResponseModel<int>> CountMyUnreadAsync();

        Task<ResponseModel> MarkAsReadAsync(long id);
        Task<ResponseModel> MarkAllMyAsReadAsync();
    }
}
