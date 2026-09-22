using Business.Interfaces;
using Core.Common;
using Core.Settings.Concrete;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Model.Dtos.Crm.Collections;
using WebAPI.Authorization;
using System.Security.Claims;
using System.Globalization;
using System.Text;

namespace WebAPI.Controllers;

[Authorize, ApiController, CollectionValidation]
[Route("api/collections/tracking")]
public sealed class CollectionTrackingController(IOptions<CollectionReadOptions> options) : ControllerBase
{
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
        await using var enumerator = result.Data.GetAsyncEnumerator(cancellationToken);
        bool hasRow;
        try
        {
            // Validate SQL translation before committing CSV response headers.
            hasRow = await enumerator.MoveNextAsync();
        }
        catch (Exception ex) when (ex is InvalidOperationException or TimeoutException or Microsoft.Data.SqlClient.SqlException)
        {
            return StatusCode(503, ResponseModel.Fail("Dışa aktarım sorgusu tamamlanamadı. Lütfen tekrar deneyin.", (Core.Enums.StatusCode)503));
        }
        Response.ContentType = "text/csv; charset=utf-8";
        Response.Headers["Content-Disposition"] = $"attachment; filename=tahsilat-takip-{query.Period:yyyy-MM}.csv";
        await using var writer = new StreamWriter(Response.Body, new UTF8Encoding(true), 16 * 1024, leaveOpen: true);
        await writer.WriteLineAsync("Grup,Abone No,Müşteri,Servis Tipi,Dönem,Vade,Para Birimi,Borç,Ödeme,Kalan,Kayıt Türü".AsMemory(), cancellationToken);
        if (hasRow) await WriteRow(enumerator.Current);
        while (await enumerator.MoveNextAsync()) await WriteRow(enumerator.Current);
        await writer.FlushAsync(cancellationToken);
        return new EmptyResult();

        Task WriteRow(CollectionTrackingItem row)
        {
            var fields = new[]
            {
                row.CustomerGroupName, row.SubscriberCode, row.CustomerName, row.ServiceTypeName,
                row.Period.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                row.DueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), row.CurrencyCode,
                row.AccruedAmount.ToString(CultureInfo.InvariantCulture),
                row.PaymentAmount.ToString(CultureInfo.InvariantCulture),
                row.RemainingAmount.ToString(CultureInfo.InvariantCulture),
                row.IsGroup ? $"{row.ContractCount} sözleşme" : row.HasAccrual ? "Borç dönemi" : "Yalnız ödeme"
            };
            return writer.WriteLineAsync(string.Join(',', fields.Select(EscapeCsv)).AsMemory(), cancellationToken);
        }
    }

    private static string EscapeCsv(string? value)
    {
        value ??= string.Empty;
        var trimmed = value.TrimStart();
        if (value.Length > 0 && "\t\r\n".Contains(value[0])
            || trimmed.Length > 0 && "=+-@".Contains(trimmed[0])
            && !decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
            value = "'" + value;
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
