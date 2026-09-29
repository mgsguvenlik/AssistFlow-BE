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

[Authorize, ApiController, CollectionValidation]
[MenuAuthorize("CollectionFollowUp", MenuPermission.View)]
[Route("api/collections/invoice-loads")]
public sealed class CollectionInvoiceLoadsController(ICollectionInvoiceLoadService service, IOptions<CollectionReadOptions> options) : ControllerBase
{
    private IActionResult Off() => StatusCode(503, ResponseModel.Fail("Tahsilat işlemi kullanıma kapalı.", (Core.Enums.StatusCode)503));
    private long Actor => long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : 0;
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct, int page = 1, int pageSize = 25)
    { if (!options.Value.Enabled) return Off(); var r = await service.ListAsync(page, pageSize, ct); return StatusCode((int)r.StatusCode, r); }
    [HttpGet("{id:long:min(1)}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    { if (!options.Value.Enabled) return Off(); var r = await service.GetAsync(id, ct); return StatusCode((int)r.StatusCode, r); }
    [HttpGet("{id:long:min(1)}/rows")]
    public async Task<IActionResult> Rows(long id, CancellationToken ct, int page = 1, int pageSize = 25, string? status = null)
    { if (!options.Value.Enabled) return Off(); var r = await service.RowsAsync(id, page, pageSize, status, ct); return StatusCode((int)r.StatusCode, r); }
    [HttpPost, RequestSizeLimit(11534336)]
    [MenuAuthorize("CollectionFollowUp", MenuPermission.Edit)]
    public async Task<IActionResult> Upload([FromForm] IFormFile file, [FromForm] string type, CancellationToken ct)
    { if (!options.Value.Enabled || !options.Value.ContractCreateEnabled) return Off(); var r = await service.UploadAsync(file, type, Actor, ct); return StatusCode((int)r.StatusCode, r); }
    [HttpPost("{id:long:min(1)}/refresh")]
    [MenuAuthorize("CollectionFollowUp", MenuPermission.Edit)]
    public Task<IActionResult> Refresh(long id, CollectionInvoiceLoadApply command, CancellationToken ct) => Process(id, command, false, ct);
    [HttpPost("{id:long:min(1)}/apply")]
    [MenuAuthorize("CollectionFollowUp", MenuPermission.Edit)]
    public Task<IActionResult> Apply(long id, CollectionInvoiceLoadApply command, CancellationToken ct) => Process(id, command, true, ct);
    private async Task<IActionResult> Process(long id, CollectionInvoiceLoadApply command, bool apply, CancellationToken ct)
    { if (!options.Value.Enabled || !options.Value.ContractCreateEnabled) return Off(); var r = await service.ProcessAsync(id, command.RowVersion, apply, Actor, ct); return StatusCode((int)r.StatusCode, r); }
}
