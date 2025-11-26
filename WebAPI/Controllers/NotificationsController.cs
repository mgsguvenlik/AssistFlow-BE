using Business.Interfaces;
using Core.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.Notification;

namespace WebAPI.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationService _svc;
        public NotificationsController(INotificationService svc) => _svc = svc;

        [HttpGet("me")]
        public Task<ResponseModel<PagedResult<NotificationGetDto>>> GetMine([FromQuery] QueryParams q)
            => _svc.GetMyAsync(q);

        [HttpGet("me/unread-count")]
        public Task<ResponseModel<int>> CountMine()
            => _svc.CountMyUnreadAsync();

        [HttpPost("{id:long}/read")]
        public Task<ResponseModel> MarkAsRead(long id)
            => _svc.MarkAsReadAsync(id);

        [HttpPost("me/read-all")]
        public Task<ResponseModel> ReadAll()
            => _svc.MarkAllMyAsReadAsync();
    }
}
