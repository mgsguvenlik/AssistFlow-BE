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
[Route("api/collections/definitions")]
public sealed class CollectionDefinitionsController(IOptions<CollectionReadOptions> options) : ControllerBase
{
    [HttpGet]
    [MenuAuthorize("CollectionFollowUp", MenuPermission.View)]
    public async Task<IActionResult> GetPage([FromQuery] CollectionDefinitionQuery query,
        [FromServices] ICollectionDefinitionReadService service, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled) return StatusCode(503,
            ResponseModel.Fail("Tahsilat tanımları henüz kullanıma açılmadı.", (Core.Enums.StatusCode)503));
        var result = await service.GetPageAsync(query, cancellationToken);
        return StatusCode((int)result.StatusCode, result);
    }
}
