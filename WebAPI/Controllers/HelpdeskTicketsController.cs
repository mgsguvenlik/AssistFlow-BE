using Business.Interfaces.Helpdesk;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.Helpdesk;

namespace WebAPI.Controllers;

[Authorize]
[ApiController]
[Route("api/helpdesk/tickets")]
public sealed class HelpdeskTicketsController(IHelpdeskTicketService service) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) { var result = await service.GetListAsync(ct); return StatusCode((int)result.StatusCode, result); }
    [HttpGet("{id:long}")] public async Task<IActionResult> Get(long id, CancellationToken ct) { var result = await service.GetAsync(id, ct); return StatusCode((int)result.StatusCode, result); }
    [HttpPost] public async Task<IActionResult> Create([FromBody] HelpdeskTicketCreateDto dto, CancellationToken ct) { var result = await service.CreateAsync(dto, ct); return StatusCode((int)result.StatusCode, result); }
    [HttpPost("{id:long}/assignments")] public async Task<IActionResult> Assign(long id, [FromBody] HelpdeskAssignmentDto dto, CancellationToken ct) { var result = await service.AssignAsync(id, dto, ct); return StatusCode((int)result.StatusCode, result); }
    [HttpPost("{id:long}/status")] public async Task<IActionResult> Status(long id, [FromBody] HelpdeskStatusChangeDto dto, CancellationToken ct) { var result = await service.ChangeStatusAsync(id, dto, ct); return StatusCode((int)result.StatusCode, result); }
    [HttpPost("{id:long}/priority")] public async Task<IActionResult> Priority(long id, [FromBody] HelpdeskPriorityChangeDto dto, CancellationToken ct) { var result = await service.ChangePriorityAsync(id, dto, ct); return StatusCode((int)result.StatusCode, result); }
    [HttpPost("{id:long}/suspend")] public async Task<IActionResult> Suspend(long id, [FromBody] HelpdeskSuspendDto dto, CancellationToken ct) { var result = await service.SuspendAsync(id, dto, ct); return StatusCode((int)result.StatusCode, result); }
    [HttpPost("{id:long}/unsuspend")] public async Task<IActionResult> Unsuspend(long id, CancellationToken ct) { var result = await service.UnsuspendAsync(id, ct); return StatusCode((int)result.StatusCode, result); }
    [HttpPost("{id:long}/comments")] public async Task<IActionResult> Comment(long id, [FromBody] HelpdeskCommentCreateDto dto, CancellationToken ct) { var result = await service.AddCommentAsync(id, dto, ct); return StatusCode((int)result.StatusCode, result); }
    [HttpGet("{id:long}/mails")] public async Task<IActionResult> Mails(long id, CancellationToken ct) { var result = await service.GetMailsAsync(id, ct); return StatusCode((int)result.StatusCode, result); }
    [HttpGet("{id:long}/comments")] public async Task<IActionResult> Comments(long id, CancellationToken ct) { var result = await service.GetCommentsAsync(id, ct); return StatusCode((int)result.StatusCode, result); }
    [HttpGet("{id:long}/history")] public async Task<IActionResult> History(long id, CancellationToken ct) { var result = await service.GetHistoryAsync(id, ct); return StatusCode((int)result.StatusCode, result); }
    [HttpPost("{id:long}/reply")] public async Task<IActionResult> Reply(long id, [FromBody] HelpdeskReplyDto dto, CancellationToken ct) { var result = await service.ReplyAsync(id, dto, ct); return StatusCode((int)result.StatusCode, result); }
}
