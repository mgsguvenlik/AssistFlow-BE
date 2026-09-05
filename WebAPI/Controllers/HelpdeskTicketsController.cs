using Business.Interfaces.Helpdesk;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.Helpdesk;
using WebAPI.Authorization;

namespace WebAPI.Controllers;

[Authorize]
[ApiController]
[Route("api/helpdesk/tickets")]
public sealed class HelpdeskTicketsController(IHelpdeskTicketService service) : ControllerBase
{
    [HttpGet] [MenuAuthorize("HelpdeskTicketList", MenuPermission.View)] public async Task<IActionResult> List([FromQuery] bool archived, CancellationToken ct) { var result = await service.GetListAsync(archived, ct); return StatusCode((int)result.StatusCode, result); }
    [HttpGet("{id:long}")] [MenuAuthorize("HelpdeskTicketList", MenuPermission.View)] public async Task<IActionResult> Get(long id, CancellationToken ct) { var result = await service.GetAsync(id, ct); return StatusCode((int)result.StatusCode, result); }
    [HttpPost("{id:long}/read")] [MenuAuthorize("HelpdeskTicketList", MenuPermission.Edit)] public async Task<IActionResult> MarkRead(long id, CancellationToken ct) { var result = await service.MarkReadAsync(id, ct); return StatusCode((int)result.StatusCode, result); }
    [HttpPost] [MenuAuthorize("HelpdeskTicketCreate", MenuPermission.Edit)] public async Task<IActionResult> Create([FromBody] HelpdeskTicketCreateDto dto, CancellationToken ct) { var result = await service.CreateAsync(dto, ct); return StatusCode((int)result.StatusCode, result); }
    [HttpPost("{id:long}/assignments")] [MenuAuthorize("HelpdeskTicketList", MenuPermission.Edit)] public async Task<IActionResult> Assign(long id, [FromBody] HelpdeskAssignmentDto dto, CancellationToken ct) { var result = await service.AssignAsync(id, dto, ct); return StatusCode((int)result.StatusCode, result); }
    [HttpPost("{id:long}/status")] [MenuAuthorize("HelpdeskTicketList", MenuPermission.Edit)] public async Task<IActionResult> Status(long id, [FromBody] HelpdeskStatusChangeDto dto, CancellationToken ct) { var result = await service.ChangeStatusAsync(id, dto, ct); return StatusCode((int)result.StatusCode, result); }
    [HttpPost("{id:long}/priority")] [MenuAuthorize("HelpdeskTicketList", MenuPermission.Edit)] public async Task<IActionResult> Priority(long id, [FromBody] HelpdeskPriorityChangeDto dto, CancellationToken ct) { var result = await service.ChangePriorityAsync(id, dto, ct); return StatusCode((int)result.StatusCode, result); }
    [HttpPost("{id:long}/suspend")] [MenuAuthorize("HelpdeskTicketList", MenuPermission.Edit)] public async Task<IActionResult> Suspend(long id, [FromBody] HelpdeskSuspendDto dto, CancellationToken ct) { var result = await service.SuspendAsync(id, dto, ct); return StatusCode((int)result.StatusCode, result); }
    [HttpPost("{id:long}/unsuspend")] [MenuAuthorize("HelpdeskTicketList", MenuPermission.Edit)] public async Task<IActionResult> Unsuspend(long id, CancellationToken ct) { var result = await service.UnsuspendAsync(id, ct); return StatusCode((int)result.StatusCode, result); }
    [HttpPost("{id:long}/comments")] [MenuAuthorize("HelpdeskTicketList", MenuPermission.Edit)] public async Task<IActionResult> Comment(long id, [FromBody] HelpdeskCommentCreateDto dto, CancellationToken ct) { var result = await service.AddCommentAsync(id, dto, ct); return StatusCode((int)result.StatusCode, result); }
    [HttpGet("{id:long}/mails")] [MenuAuthorize("HelpdeskTicketList", MenuPermission.View)] public async Task<IActionResult> Mails(long id, CancellationToken ct) { var result = await service.GetMailsAsync(id, ct); return StatusCode((int)result.StatusCode, result); }
    [HttpGet("{id:long}/comments")] [MenuAuthorize("HelpdeskTicketList", MenuPermission.View)] public async Task<IActionResult> Comments(long id, CancellationToken ct) { var result = await service.GetCommentsAsync(id, ct); return StatusCode((int)result.StatusCode, result); }
    [HttpGet("{id:long}/history")] [MenuAuthorize("HelpdeskTicketList", MenuPermission.View)] public async Task<IActionResult> History(long id, CancellationToken ct) { var result = await service.GetHistoryAsync(id, ct); return StatusCode((int)result.StatusCode, result); }
    [HttpPost("{id:long}/reply")] [MenuAuthorize("HelpdeskTicketList", MenuPermission.Edit)] public async Task<IActionResult> Reply(long id, [FromBody] HelpdeskReplyDto dto, CancellationToken ct) { var result = await service.ReplyAsync(id, dto, ct); return StatusCode((int)result.StatusCode, result); }
}
