using Business.Interfaces;
using Business.Services.Crm.Collections;
using Core.Common;
using Core.Settings.Concrete;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Model.Dtos.Crm.Collections;
using WebAPI.Authorization;
using System.Security.Claims;

namespace WebAPI.Controllers;

[Authorize, ApiController, CollectionValidation]
[Route("api/collections/tracking")]
public sealed class CollectionTrackingController(IOptions<CollectionReadOptions> options) : ControllerBase
{
    [HttpGet("totals")]
    [MenuAuthorize("CollectionFollowUp", MenuPermission.View)]
    public async Task<IActionResult> Totals([FromQuery] CollectionTrackingQuery query,
        [FromServices] ICollectionTrackingService service, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
            return StatusCode(503, ResponseModel.Fail("Tahsilat modülü kullanıma kapalı.", (Core.Enums.StatusCode)503));
        var result = await service.GetTotalsAsync(query, cancellationToken);
        return StatusCode((int)result.StatusCode, result);
    }
    [HttpGet("group-status-history")]
    [MenuAuthorize("CollectionFollowUp", MenuPermission.View)]
    public async Task<IActionResult> GetGroupHistory([FromQuery] CollectionGroupHistoryQuery query,
        [FromServices] ICollectionGroupFollowUpService service, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
            return StatusCode(503, ResponseModel.Fail("Tahsilat modülü kullanıma kapalı.", (Core.Enums.StatusCode)503));
        var result = await service.GetHistoryAsync(query, cancellationToken);
        return StatusCode((int)result.StatusCode, result);
    }

    [HttpGet("{contractId:long:min(1)}/group-status")]
    [MenuAuthorize("CollectionFollowUp", MenuPermission.View)]
    public async Task<IActionResult> GetGroupStatus(long contractId, [FromQuery] DateOnly period,
        [FromServices] ICollectionGroupFollowUpService service, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
            return StatusCode(503, ResponseModel.Fail("Tahsilat takibi henüz kullanıma açılmadı.", (Core.Enums.StatusCode)503));
        var result = await service.GetAsync(contractId, period, cancellationToken);
        return StatusCode((int)result.StatusCode, result);
    }

    [HttpPost("{contractId:long:min(1)}/group-status")]
    [MenuAuthorize("CollectionFollowUp", MenuPermission.Edit)]
    public async Task<IActionResult> SaveGroupStatus(long contractId, [FromBody] CollectionGroupFollowUpUpdate command,
        [FromServices] ICollectionGroupFollowUpService service, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
            return StatusCode(503, ResponseModel.Fail("Tahsilat takibi henüz kullanıma açılmadı.", (Core.Enums.StatusCode)503));
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!long.TryParse(claim, out var actorId) || actorId <= 0)
            return Unauthorized(ResponseModel.Fail("Geçerli kullanıcı kimliği bulunamadı.", Core.Enums.StatusCode.Unauthorized));
        var result = await service.SaveAsync(contractId, command, actorId, cancellationToken);
        return StatusCode((int)result.StatusCode, result);
    }

    [HttpGet]
    [MenuAuthorize("CollectionFollowUp", MenuPermission.View)]
    public async Task<IActionResult> GetPage([FromQuery] CollectionTrackingQuery query,
        [FromServices] ICollectionTrackingService service, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
            return StatusCode(503, ResponseModel.Fail("Tahsilat takibi henüz kullanıma açılmadı.", (Core.Enums.StatusCode)503));
        var result = await service.GetPageAsync(query, cancellationToken);
        return StatusCode((int)result.StatusCode, result);
    }

    [HttpGet("export")]
    [MenuAuthorize("CollectionFollowUp", MenuPermission.View)]
    public async Task<IActionResult> Export([FromQuery] CollectionTrackingQuery query,
        [FromServices] ICollectionTrackingService service, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
            return StatusCode(503, ResponseModel.Fail("Tahsilat takibi henüz kullanıma açılmadı.", (Core.Enums.StatusCode)503));
        var result = service.GetExportRows(query);
        if (!result.IsSuccess || result.Data is null)
            return StatusCode((int)result.StatusCode, result);
        try
        {
            var stream = await CollectionTrackingExcelExporter.CreateAsync(result.Data, cancellationToken);
            var periodLabel = query.PeriodFrom is { } from ? $"{from:yyyy-MM}_{query.Period:yyyy-MM}" : $"{query.Period:yyyy-MM}";
            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"tahsilat-takip-{periodLabel}.xlsx");
        }
        catch (Exception ex) when (ex is InvalidOperationException or TimeoutException or Microsoft.Data.SqlClient.SqlException)
        {
            return StatusCode(503, ResponseModel.Fail("Dışa aktarım sorgusu tamamlanamadı. Lütfen tekrar deneyin.", (Core.Enums.StatusCode)503));
        }
    }
}
