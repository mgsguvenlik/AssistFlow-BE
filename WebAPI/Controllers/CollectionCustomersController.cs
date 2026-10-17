using System.Security.Claims;
using Business.Interfaces;
using Core.Common;
using Core.Settings.Concrete;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Model.Dtos.Crm.Collections;
using WebAPI.Authorization;

namespace WebAPI.Controllers;

[Authorize]
[ApiController]
[CollectionValidation]
[Route("api/collections/customers")]
public sealed class CollectionCustomersController(IOptions<CollectionReadOptions> options, ICollectionCustomerService service) : ControllerBase
{
    private IActionResult Unavailable() => StatusCode(503, ResponseModel.Fail("Tahsilat işlemi kullanıma kapalı.", (Core.Enums.StatusCode)503));

    [HttpGet("{id:long:min(1)}/attachments")]
    [MenuAuthorize("CollectionFollowUp", MenuPermission.View)]
    public async Task<IActionResult> Attachments(long id, [FromServices] ICollectionContractAttachmentService attachments,
        CancellationToken ct, int page = 1, int pageSize = 25)
    {
        if (!options.Value.Enabled) return Unavailable();
        var result = await attachments.GetCustomerPageAsync(id, page, pageSize, ct);
        return StatusCode((int)result.StatusCode, result);
    }

    [HttpPost("{id:long:min(1)}/attachments")]
    [MenuAuthorize("CollectionFollowUp", MenuPermission.Edit)]
    [RequestSizeLimit(22 * 1024 * 1024)]
    public async Task<IActionResult> UploadAttachment(long id, [FromForm] IFormFile file, [FromForm] Guid requestId,
        [FromServices] ICollectionContractAttachmentService attachments, CancellationToken ct)
    {
        if (!options.Value.Enabled || !options.Value.ContractCreateEnabled) return Unavailable();
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var actor) || actor <= 0)
            return Unauthorized(ResponseModel.Fail("Geçerli kullanıcı kimliği bulunamadı."));
        var result = await attachments.UploadCustomerAsync(id, file, requestId, actor, ct);
        return StatusCode((int)result.StatusCode, result);
    }
    [HttpPost("{id:long:min(1)}/attachments/{attachmentId:long:min(1)}/remove")]
    [MenuAuthorize("CollectionFollowUp", MenuPermission.Edit)]
    public async Task<IActionResult> RemoveAttachment(long id, long attachmentId, [FromServices] ICollectionContractAttachmentService attachments, CancellationToken ct)
    {
        if (!options.Value.Enabled || !options.Value.ContractCreateEnabled) return Unavailable();
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var actor) || actor <= 0)
            return Unauthorized(ResponseModel.Fail("Geçerli kullanıcı kimliği bulunamadı."));
        var result = await attachments.RemoveCustomerAsync(id, attachmentId, actor, ct);
        return StatusCode((int)result.StatusCode, result);
    }
    [HttpGet("{id:long:min(1)}")]
    [MenuAuthorize("CollectionFollowUp", MenuPermission.View)]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        if (!options.Value.Enabled) return Unavailable();
        var result = await service.GetAsync(id, ct);
        return StatusCode((int)result.StatusCode, result);
    }
    [HttpGet("{id:long:min(1)}/notes")]
    [MenuAuthorize("CollectionFollowUp", MenuPermission.View)]
    public async Task<IActionResult> Notes(long id, [FromQuery] CollectionRateHistoryQuery query, CancellationToken ct)
    {
        if (!options.Value.Enabled) return Unavailable();
        var result = await service.NotesAsync(id, query, ct);
        return StatusCode((int)result.StatusCode, result);
    }
    [HttpGet("{id:long:min(1)}/payments")]
    [MenuAuthorize("CollectionFollowUp", MenuPermission.View)]
    public async Task<IActionResult> Payments(long id, [FromQuery] CollectionRateHistoryQuery query, CancellationToken ct)
    {
        if (!options.Value.Enabled) return Unavailable();
        var result = await service.PaymentsAsync(id, query, ct);
        return StatusCode((int)result.StatusCode, result);
    }
    [HttpPost("{id:long:min(1)}/notes")]
    [MenuAuthorize("CollectionFollowUp", MenuPermission.Edit)]
    public async Task<IActionResult> AddNote(long id, [FromBody] CollectionCustomerNoteCreate command, CancellationToken ct)
    {
        if (!options.Value.Enabled || !options.Value.ContractCreateEnabled) return Unavailable();
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var actor) || actor <= 0)
            return Unauthorized(ResponseModel.Fail("Geçerli kullanıcı kimliği bulunamadı."));
        var result = await service.AddNoteAsync(id, command, actor, ct);
        return StatusCode((int)result.StatusCode, result);
    }
    [HttpPost("{id:long:min(1)}/notes/{noteId:long:min(1)}/update")]
    [MenuAuthorize("CollectionFollowUp", MenuPermission.Edit)]
    public Task<IActionResult> UpdateNote(long id, long noteId, [FromBody] CollectionCustomerNoteChange command, CancellationToken ct)
        => Change(id, noteId, command, false, ct);

    [HttpPost("{id:long:min(1)}/notes/{noteId:long:min(1)}/delete")]
    [MenuAuthorize("CollectionFollowUp", MenuPermission.Edit)]
    public Task<IActionResult> DeleteNote(long id, long noteId, [FromBody] CollectionCustomerNoteChange command, CancellationToken ct)
        => Change(id, noteId, command, true, ct);

    private async Task<IActionResult> Change(long id, long noteId, CollectionCustomerNoteChange command, bool delete, CancellationToken ct)
    {
        if (!options.Value.Enabled || !options.Value.ContractCreateEnabled) return Unavailable();
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var actor) || actor <= 0)
            return Unauthorized(ResponseModel.Fail("Geçerli kullanıcı kimliği bulunamadı."));
        var result = await service.ChangeNoteAsync(id, noteId, command, delete, actor, ct);
        return StatusCode((int)result.StatusCode, result);
    }
}
