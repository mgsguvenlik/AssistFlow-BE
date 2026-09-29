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
[MenuAuthorize("CollectionFollowUp", MenuPermission.View)]
[Route("api/collections/invoices")]
public sealed class CollectionInvoicesController(IOptions<CollectionReadOptions> options, ICollectionInvoiceService service, ICollectionInvoiceCommandService commands) : ControllerBase
{
    private IActionResult Unavailable() => StatusCode(503, ResponseModel.Fail("Tahsilat işlemi kullanıma kapalı.", (Core.Enums.StatusCode)503));

    [HttpPost("{id:long:min(1)}/payments")]
    [MenuAuthorize("CollectionFollowUp", MenuPermission.Edit)]
    public Task<IActionResult> CreatePayment(long id, CollectionInvoiceCommand command, CancellationToken ct) => Execute(id, null, "PaymentCreate", command, ct);

    [HttpPost("{id:long:min(1)}/payments/{paymentId:long:min(1)}/update")]
    [MenuAuthorize("CollectionFollowUp", MenuPermission.Edit)]
    public Task<IActionResult> UpdatePayment(long id, long paymentId, CollectionInvoiceCommand command, CancellationToken ct) => Execute(id, paymentId, "PaymentUpdate", command, ct);

    [HttpPost("{id:long:min(1)}/payments/{paymentId:long:min(1)}/delete")]
    [MenuAuthorize("CollectionFollowUp", MenuPermission.Edit)]
    public Task<IActionResult> DeletePayment(long id, long paymentId, CollectionInvoiceCommand command, CancellationToken ct) => Execute(id, paymentId, "PaymentDelete", command, ct);

    [HttpPost("{id:long:min(1)}/comment")]
    [MenuAuthorize("CollectionFollowUp", MenuPermission.Edit)]
    public Task<IActionResult> Comment(long id, CollectionInvoiceCommand command, CancellationToken ct) => Execute(id, null, "CommentUpdate", command, ct);

    [HttpPost("{id:long:min(1)}/delete")]
    [MenuAuthorize("CollectionFollowUp", MenuPermission.Edit)]
    public Task<IActionResult> Delete(long id, CollectionInvoiceCommand command, CancellationToken ct) => Execute(id, null, "InvoiceDelete", command, ct);

    private async Task<IActionResult> Execute(long id, long? paymentId, string kind, CollectionInvoiceCommand command, CancellationToken ct)
    {
        if (!options.Value.Enabled || !options.Value.ContractCreateEnabled) return Unavailable();
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var actor) || actor <= 0)
            return Unauthorized(ResponseModel.Fail("Geçerli kullanıcı kimliği bulunamadı."));
        var result = await commands.ExecuteAsync(id, paymentId, kind, command, actor, ct);
        return StatusCode((int)result.StatusCode, result);
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] CollectionInvoiceQuery query, CancellationToken ct)
    {
        if (!options.Value.Enabled) return Unavailable();
        var result = await service.ListAsync(query, ct);
        return StatusCode((int)result.StatusCode, result);
    }
    [HttpGet("{id:long:min(1)}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        if (!options.Value.Enabled) return Unavailable();
        var result = await service.GetAsync(id, ct);
        return StatusCode((int)result.StatusCode, result);
    }
    [HttpGet("{id:long:min(1)}/payments")]
    public async Task<IActionResult> Payments(long id, [FromQuery] CollectionRateHistoryQuery query, CancellationToken ct)
    {
        if (!options.Value.Enabled) return Unavailable();
        var result = await service.PaymentsAsync(id, query, ct);
        return StatusCode((int)result.StatusCode, result);
    }
}
