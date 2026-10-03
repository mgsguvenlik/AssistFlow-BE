using System.Security.Claims;
using Business.Services.Crm.Collections;
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
[Route("api/collections/contracts/{id:long:min(1)}/sms")]
public sealed class CollectionSmsController(IOptions<CollectionReadOptions> options, CollectionSmsService service) : ControllerBase
{
    [HttpGet("/api/collections/sms")]
    [MenuAuthorize("CollectionFollowUp", MenuPermission.View)]
    public async Task<IActionResult> Tracking([FromQuery] CollectionSmsTrackingQuery query, CancellationToken ct)
    {
        if (!options.Value.Enabled) return Unavailable();
        var r = await service.TrackingAsync(query, ct); return StatusCode((int)r.StatusCode, r);
    }

    [HttpGet("preview")]
    [MenuAuthorize("CollectionFollowUp", MenuPermission.View)]
    public async Task<IActionResult> Preview(long id, CancellationToken ct)
    {
        if (!options.Value.Enabled) return Unavailable();
        var r = await service.PreviewAsync(id, ct); return StatusCode((int)r.StatusCode, r);
    }

    [HttpGet]
    [MenuAuthorize("CollectionFollowUp", MenuPermission.View)]
    public async Task<IActionResult> History(long id, CancellationToken ct, int page = 1, int pageSize = 25, byte? status = null)
    {
        if (!options.Value.Enabled) return Unavailable();
        var r = await service.HistoryAsync(id, page, pageSize, status, ct); return StatusCode((int)r.StatusCode, r);
    }

    [HttpPost]
    [MenuAuthorize("CollectionFollowUp", MenuPermission.Edit)]
    public async Task<IActionResult> Send(long id, CollectionSmsSendCommand command, CancellationToken ct)
    {
        if (!options.Value.Enabled || !options.Value.ContractCreateEnabled) return Unavailable();
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!long.TryParse(claim, out var actor) || actor <= 0)
            return Unauthorized(ResponseModel.Fail("Geçerli kullanıcı gereklidir.", Core.Enums.StatusCode.Unauthorized));
        var r = await service.SendAsync(id, command, actor, ct); return StatusCode((int)r.StatusCode, r);
    }

    private IActionResult Unavailable() => StatusCode(503, ResponseModel.Fail("Tahsilat işlemleri henüz kullanıma açılmadı.", (Core.Enums.StatusCode)503));
}
