using System.Collections.Concurrent;
using System.Security.Claims;
using Business.Interfaces.Sheets;
using Business.Services.Sheets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace WebAPI.Hubs;

[Authorize]
public sealed class SheetsHub(ISheetsService sheets, IHubContext<SheetsHub> hub) : Hub
{
    private sealed record Presence(Guid WorkbookId, long UserId, string Name, Guid? WorksheetId, int? Row, int? Column);
    private static readonly ConcurrentDictionary<string, Presence> Members = new();
    public static string Group(Guid id) => "sheets:" + id;
    public async Task Join(Guid workbookId)
    {
        await Check(workbookId);
        if (!long.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? Context.User?.FindFirstValue("sub"), out var userId))
            throw new HubException("Kullanıcı bulunamadı.");
        if (Members.TryRemove(Context.ConnectionId, out var old))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, Group(old.WorkbookId));
            await Broadcast(old.WorkbookId);
        }
        Members[Context.ConnectionId] = new(workbookId, userId, Context.User?.Identity?.Name ?? "Kullanıcı", null, null, null);
        await Groups.AddToGroupAsync(Context.ConnectionId, Group(workbookId));
        await Broadcast(workbookId);
    }
    public async Task Focus(Guid workbookId, Guid worksheetId, int row, int column)
    {
        var book = await Check(workbookId);
        if (!Members.TryGetValue(Context.ConnectionId, out var presence) || presence.WorkbookId != workbookId) throw new HubException("Önce tabloya katılın.");
        var sheet = book.Worksheets.SingleOrDefault(x => x.Id == worksheetId);
        if (sheet == null || row < 0 || row >= sheet.Rows || column < 0 || column >= sheet.Columns) return;
        Members[Context.ConnectionId] = presence with { WorksheetId = worksheetId, Row = row, Column = column };
        await Broadcast(workbookId);
    }
    private async Task<Model.Dtos.Sheets.SheetWorkbookDto> Check(Guid id)
    {
        try { return await sheets.OpenAsync(id, null, Context.ConnectionAborted); }
        catch (SheetRuleException ex)
        {
            Members.TryRemove(Context.ConnectionId, out _);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, Group(id));
            throw new HubException(ex.Message);
        }
    }
    private async Task Broadcast(Guid id)
    {
        await PruneAsync(sheets, hub, id, CancellationToken.None);
        await Clients.Group(Group(id)).SendAsync("Presence", Members.Values.Where(x => x.WorkbookId == id));
    }
    public static async Task PruneAsync(ISheetsService sheets, IHubContext<SheetsHub> hub, Guid id, CancellationToken ct)
    {
        foreach (var entry in Members.Where(x => x.Value.WorkbookId == id).ToArray())
            if (!await sheets.CanViewAsync(id, entry.Value.UserId, ct))
            {
                Members.TryRemove(entry.Key, out _);
                await hub.Groups.RemoveFromGroupAsync(entry.Key, Group(id), ct);
                await hub.Clients.Client(entry.Key).SendAsync("AccessChanged", cancellationToken: ct);
            }
    }
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Members.TryRemove(Context.ConnectionId, out var presence)) await Broadcast(presence.WorkbookId);
        await base.OnDisconnectedAsync(exception);
    }
}
