using Business.Interfaces.Helpdesk;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Model.Dtos.Helpdesk;
using WebAPI.Authorization;

namespace WebAPI.Controllers;

[Authorize]
[ApiController]
[Route("api/helpdesk/mailboxes")]
public sealed class HelpdeskMailboxesController(IHelpdeskMailboxService service) : ControllerBase
{
    [HttpGet]
    [MenuAuthorize("HelpdeskMailbox", MenuPermission.View)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await service.GetMailboxesAsync(ct);
        return StatusCode((int)result.StatusCode, result);
    }

    [HttpPost]
    [MenuAuthorize("HelpdeskMailbox", MenuPermission.Edit)]
    public async Task<IActionResult> Create([FromBody] HelpdeskMailboxCreateDto dto, CancellationToken ct)
    {
        var result = await service.CreateMailboxAsync(dto, ct);
        return StatusCode((int)result.StatusCode, result);
    }

    [HttpPost("{id:long}/update")]
    [MenuAuthorize("HelpdeskMailbox", MenuPermission.Edit)]
    public async Task<IActionResult> Update(long id, [FromBody] HelpdeskMailboxUpdateDto dto, CancellationToken ct)
    {
        var result = await service.UpdateMailboxAsync(id, dto, ct);
        return StatusCode((int)result.StatusCode, result);
    }

    [HttpPost("{id:long}/delete")]
    [MenuAuthorize("HelpdeskMailbox", MenuPermission.Edit)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var result = await service.DeleteMailboxAsync(id, ct);
        return StatusCode((int)result.StatusCode, result);
    }

        [HttpGet("{mailboxId:long}/rules")]
        [MenuAuthorize(new[] { "HelpdeskMailbox", "HelpdeskMailRules" }, MenuPermission.View)]
    public async Task<IActionResult> Rules(long mailboxId, CancellationToken ct)
    {
        var result = await service.GetRulesAsync(mailboxId, ct);
        return StatusCode((int)result.StatusCode, result);
    }

        [HttpPost("rules")]
        [MenuAuthorize(new[] { "HelpdeskMailbox", "HelpdeskMailRules" }, MenuPermission.Edit)]
    public async Task<IActionResult> CreateRule([FromBody] HelpdeskMailRuleCreateDto dto, CancellationToken ct)
    {
        var result = await service.CreateRuleAsync(dto, ct);
        return StatusCode((int)result.StatusCode, result);
    }

        [HttpPost("rules/{id:long}/update")]
        [MenuAuthorize(new[] { "HelpdeskMailbox", "HelpdeskMailRules" }, MenuPermission.Edit)]
    public async Task<IActionResult> UpdateRule(long id, [FromBody] HelpdeskMailRuleUpdateDto dto, CancellationToken ct)
    {
        var result = await service.UpdateRuleAsync(id, dto, ct);
        return StatusCode((int)result.StatusCode, result);
    }

        [HttpPost("rules/{id:long}/delete")]
        [MenuAuthorize(new[] { "HelpdeskMailbox", "HelpdeskMailRules" }, MenuPermission.Edit)]
    public async Task<IActionResult> DeleteRule(long id, CancellationToken ct)
    {
        var result = await service.DeleteRuleAsync(id, ct);
        return StatusCode((int)result.StatusCode, result);
    }
}
