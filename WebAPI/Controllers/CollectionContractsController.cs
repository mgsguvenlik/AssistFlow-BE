using Business.Interfaces;
using Core.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Model.Dtos.Crm.Collections;
using WebAPI.Authorization;
using Core.Settings.Concrete;

namespace WebAPI.Controllers;

[Authorize]
[ApiController]
[Route("api/collections/contracts")]
public sealed class CollectionContractsController(IOptions<CollectionReadOptions> options) : ControllerBase
{
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

    private ObjectResult Unavailable() => StatusCode(503,
        ResponseModel.Fail("Tahsilat sözleşme görüntüleme henüz kullanıma açılmadı.", (Core.Enums.StatusCode)503));
}
