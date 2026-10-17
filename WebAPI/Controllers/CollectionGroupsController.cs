using Business.Interfaces;
using Core.Common;
using Core.Settings.Concrete;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WebAPI.Authorization;

namespace WebAPI.Controllers;

[Authorize]
[ApiController]
[CollectionValidation]
[Route("api/collections/groups")]
public sealed class CollectionGroupsController(IOptions<CollectionReadOptions> options, ICollectionGroupContextService service) : ControllerBase
{
    [HttpGet("{id:long:min(1)}/context")]
    [MenuAuthorize("CollectionFollowUp", MenuPermission.View)]
    public async Task<IActionResult> GetContext(long id, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled) return StatusCode(503, ResponseModel.Fail("Tahsilat modülü kullanıma kapalı.", (Core.Enums.StatusCode)503));
        var result = await service.GetAsync(id, cancellationToken);
        return StatusCode((int)result.StatusCode, result);
    }

    [HttpGet("resolve-account")]
    [MenuAuthorize("CollectionFollowUp", MenuPermission.View)]
    public async Task<IActionResult> ResolveAccount([FromQuery] string accountNo, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled) return StatusCode(503, ResponseModel.Fail("Tahsilat modülü kullanıma kapalı.", (Core.Enums.StatusCode)503));
        var result = await service.ResolveAccountAsync(accountNo, cancellationToken);
        return StatusCode((int)result.StatusCode, result);
    }
}
