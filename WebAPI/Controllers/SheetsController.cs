using Business.Interfaces.Sheets;
using Business.Services.Sheets;
using Core.Common;
using Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Model.Dtos.Sheets;
using WebAPI.Hubs;

namespace WebAPI.Controllers;

[Authorize]
[ApiController]
[Route("api/Sheets")]
public sealed class SheetsController(ISheetsService sheets, IHubContext<SheetsHub> hub, ILogger<SheetsController> logger) : ControllerBase
{
    private async Task<IActionResult> Run<T>(Func<Task<T>> action)
    {
        try { return Ok(ResponseModel<T>.Success(await action())); }
        catch (SheetRuleException ex) { return StatusCode(ex.Status, ResponseModel<T>.Fail(ex.Message, (StatusCode)ex.Status)); }
    }
    private Task<IActionResult> Run(Func<Task> action) => Run(async () => { await action(); return true; });

    // Publication is committed before notification. A transport failure must not report a failed save.
    private async Task Notify(Guid id, Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex) { logger.LogWarning(ex, "Sheets notification failed for workbook {WorkbookId}", id); }
    }

    [HttpGet]
    public Task<IActionResult> List([FromQuery] SheetQuery query, CancellationToken ct) => Run(() => sheets.ListAsync(query, ct));
    [HttpGet("users")]
    public Task<IActionResult> Users([FromQuery] QueryParams query, CancellationToken ct) => Run(() => sheets.EligibleUsersAsync(query, ct));
    [HttpPost]
    public Task<IActionResult> Create(SheetCreateDto dto, CancellationToken ct) => Run(() => sheets.CreateAsync(dto, ct));
    [HttpPost("import")]
    [RequestSizeLimit(536870912)]
    [RequestFormLimits(MultipartBodyLengthLimit = 536870912)]
    public Task<IActionResult> CreateFromExcel(IFormFile file, [FromForm] string name, CancellationToken ct) => Run(async () =>
    {
        if (!Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase) || file.Length == 0)
            throw new SheetRuleException("Dolu bir .xlsx dosyası seçin.");
        await using var stream = file.OpenReadStream();
        return await sheets.CreateFromExcelAsync(name, stream, ct);
    });
    [HttpGet("{id:guid}")]
    public Task<IActionResult> Open(Guid id, [FromQuery] Guid? revisionId, CancellationToken ct) => Run(() => sheets.OpenAsync(id, revisionId, ct));
    [HttpPost("{id:guid}/window")]
    public Task<IActionResult> Window(Guid id, SheetWindowQuery query, CancellationToken ct) => Run(() => sheets.WindowAsync(id, query, ct));
    [HttpPost("{id:guid}/save")]
    public Task<IActionResult> Save(Guid id, SheetSaveDto dto, CancellationToken ct) => Run(async () =>
    {
        var result = await sheets.SaveAsync(id, dto, ct);
        await Notify(id, async () =>
        {
            await SheetsHub.PruneAsync(sheets, hub, id, ct);
            await hub.Clients.Group(SheetsHub.Group(id)).SendAsync("WorkbookSaved", new { result.RevisionId }, ct);
        });
        return result;
    });
    [HttpPost("{id:guid}/delete")]
    public Task<IActionResult> Delete(Guid id, CancellationToken ct) => Run(async () =>
    {
        await sheets.DeleteAsync(id, ct);
        await Notify(id, () => hub.Clients.Group(SheetsHub.Group(id)).SendAsync("WorkbookDeleted", cancellationToken: ct));
    });
    [HttpGet("{id:guid}/access")]
    public Task<IActionResult> Access(Guid id, CancellationToken ct) => Run(() => sheets.AccessAsync(id, ct));
    [HttpPost("{id:guid}/access/{userId:long}")]
    public Task<IActionResult> Grant(Guid id, long userId, [FromQuery] bool grant, CancellationToken ct) => Run(async () =>
    {
        await sheets.GrantAsync(id, userId, grant, ct);
        await Notify(id, async () =>
        {
            await SheetsHub.PruneAsync(sheets, hub, id, ct);
            await hub.Clients.Group(SheetsHub.Group(id)).SendAsync("AccessChanged", cancellationToken: ct);
        });
    });
    [HttpGet("{id:guid}/activities")]
    public Task<IActionResult> Activities(Guid id, [FromQuery] QueryParams query, CancellationToken ct) => Run(() => sheets.ActivitiesAsync(id, query, ct));

    [HttpPost("{id:guid}/import")]
    [RequestSizeLimit(536870912)]
    [RequestFormLimits(MultipartBodyLengthLimit = 536870912)]
    public Task<IActionResult> Import(Guid id, IFormFile file, [FromForm] string draft, CancellationToken ct) => Run(async () =>
    {
        if (!Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase) || file.Length == 0)
            throw new SheetRuleException("Dolu bir .xlsx dosyası seçin.");
        SheetSaveDto dto;
        try { dto = SheetSnapshotEngine.Deserialize<SheetSaveDto>(draft); }
        catch (System.Text.Json.JsonException) { throw new SheetRuleException("İçe aktarma taslağı geçersiz."); }
        await using var stream = file.OpenReadStream();
        return await sheets.ImportAsync(id, dto, stream, ct);
    });

    [HttpGet("{id:guid}/export")]
    public async Task<IActionResult> Export(Guid id, [FromQuery] Guid? revisionId, CancellationToken ct)
    {
        try
        {
            var path = await sheets.ExportAsync(id, revisionId, ct);
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                65536, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "MGS-Tablolar.xlsx");
        }
        catch (SheetRuleException ex) { return StatusCode(ex.Status, ResponseModel.Fail(ex.Message, (StatusCode)ex.Status)); }
    }
}
