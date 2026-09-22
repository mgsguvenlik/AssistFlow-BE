using Business.Interfaces;
using Core.Common;
using Core.Settings.Concrete;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Model.Dtos.Crm.Collections;
using WebAPI.Authorization;

namespace WebAPI.Controllers;

[Authorize, ApiController, CollectionValidation]
[Route("api/collections/reports")]
public sealed class CollectionReportsController(IOptions<CollectionReadOptions> options) : ControllerBase
{
    [HttpGet("contracts")]
    [MenuAuthorize("CollectionFollowUp", MenuPermission.View)]
    public async Task<IActionResult> GetContracts(
        [FromQuery] CollectionContractReportQuery query,
        [FromServices] ICollectionContractReportService service,
        CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
            return StatusCode(503, ResponseModel.Fail(
                "Tahsilat raporları henüz kullanıma açılmadı.", (Core.Enums.StatusCode)503));
        var result = await service.GetPageAsync(query, cancellationToken);
        return StatusCode((int)result.StatusCode, result);
    }

    [HttpGet("payments")]
    [MenuAuthorize("CollectionFollowUp", MenuPermission.View)]
    public async Task<IActionResult> GetPayments(
        [FromQuery] CollectionPaymentReportQuery query,
        [FromServices] ICollectionPaymentReportService service,
        CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
            return StatusCode(503, ResponseModel.Fail(
                "Tahsilat raporları henüz kullanıma açılmadı.", (Core.Enums.StatusCode)503));
        var result = await service.GetPageAsync(query, cancellationToken);
        return StatusCode((int)result.StatusCode, result);
    }
}
