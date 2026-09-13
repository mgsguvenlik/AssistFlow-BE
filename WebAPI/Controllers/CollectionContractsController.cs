using Business.Interfaces;
using Core.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Model.Dtos.Crm.Collections;
using WebAPI.Authorization;
using Core.Settings.Concrete;
using System.Security.Claims;

namespace WebAPI.Controllers;

[Authorize]
[ApiController]
[CollectionValidation]
[Route("api/collections/contracts")]
public sealed class CollectionContractsController(IOptions<CollectionReadOptions> options) : ControllerBase
{
    [HttpPost]
    [MenuAuthorize("CollectionFollowUp", MenuPermission.Edit)]
    public async Task<IActionResult> Create([FromBody] CollectionContractCreate command,
        [FromServices] ICollectionContractCreateService service, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled || !options.Value.ContractCreateEnabled) return Unavailable();
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!long.TryParse(claim, out var actorId) || actorId <= 0)
            return Unauthorized(ResponseModel.Fail("Geçerli kullanıcı kimliği bulunamadı.", Core.Enums.StatusCode.Unauthorized));
        var result = await service.CreateAsync(command, actorId, cancellationToken);
        return StatusCode((int)result.StatusCode, result);
    }
    [HttpGet]
    [MenuAuthorize("CollectionFollowUp", MenuPermission.View)]
    public async Task<IActionResult> GetPage([FromQuery] CollectionContractQuery query,
        [FromServices] ICollectionContractReadService service, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled) return Unavailable();
        var result = await service.GetPageAsync(query, cancellationToken);
        return StatusCode((int)result.StatusCode, result);
    }

    [HttpGet("{id:long:min(1)}")]
    [MenuAuthorize("CollectionFollowUp", MenuPermission.View)]
    public async Task<IActionResult> GetDetail(long id,
        [FromServices] ICollectionContractReadService service, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled) return Unavailable();
        var result = await service.GetDetailAsync(id, cancellationToken);
        return StatusCode((int)result.StatusCode, result);
    }

    [HttpGet("{id:long:min(1)}/rate-history")]
    [MenuAuthorize("CollectionFollowUp", MenuPermission.View)]
    public async Task<IActionResult> GetHistory(long id, [FromQuery] CollectionRateHistoryQuery query,
        [FromServices] ICollectionContractReadService service, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled) return Unavailable();
        var result = await service.GetHistoryAsync(id, query, cancellationToken);
        return StatusCode((int)result.StatusCode, result);
    }

    [HttpPost("{id:long:min(1)}/subscription")]
    [MenuAuthorize("CollectionFollowUp", MenuPermission.Edit)]
    public async Task<IActionResult> ChangeSubscription(long id, [FromBody] CollectionSubscriptionChange command,
        [FromServices] ICollectionSubscriptionService service, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled || !options.Value.ContractCreateEnabled) return Unavailable();
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!long.TryParse(claim, out var actorId) || actorId <= 0)
            return Unauthorized(ResponseModel.Fail("Geçerli kullanıcı kimliği bulunamadı.", Core.Enums.StatusCode.Unauthorized));
        var result = await service.ChangeAsync(id, command, actorId, cancellationToken);
        return StatusCode((int)result.StatusCode, result);
    }

    [HttpPatch("{id:long:min(1)}/identity")]
    [MenuAuthorize("CollectionFollowUp", MenuPermission.Edit)]
    public async Task<IActionResult> UpdateIdentity(long id, [FromBody] CollectionContractIdentityUpdate command,
        [FromServices] ICollectionContractUpdateService service, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled || !options.Value.ContractCreateEnabled) return Unavailable();
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!long.TryParse(claim, out var actorId) || actorId <= 0)
            return Unauthorized(ResponseModel.Fail("Geçerli kullanıcı kimliği bulunamadı.", Core.Enums.StatusCode.Unauthorized));
        var result = await service.UpdateIdentityAsync(id, command, actorId, cancellationToken);
        return StatusCode((int)result.StatusCode, result);
    }

    private ObjectResult Unavailable() => StatusCode(503,
        ResponseModel.Fail("Tahsilat sözleşme görüntüleme henüz kullanıma açılmadı.", (Core.Enums.StatusCode)503));
}
