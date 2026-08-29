using Business.Interfaces.Helpdesk;
using Data.Concrete.EfCore.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Model.Concrete.Helpdesk;

namespace Business.Services.Helpdesk;

public sealed class HelpdeskBackgroundWorker(IServiceProvider serviceProvider, ILogger<HelpdeskBackgroundWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Helpdesk background worker döngüsü başarısız oldu.");
            }
            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDataContext>();
        await ReactivateExpiredTicketsAsync(db, ct);

        var mailboxes = await db.HelpdeskMailboxes.AsNoTracking()
            .Where(x => x.IsActive && !x.IsDeleted).OrderBy(x => x.Id).ToListAsync(ct);
        var protector = scope.ServiceProvider.GetRequiredService<IHelpdeskSecretProtector>();
        var client = scope.ServiceProvider.GetRequiredService<IHelpdeskMailboxClient>();
        var processor = scope.ServiceProvider.GetRequiredService<IHelpdeskIncomingMailProcessor>();

        foreach (var mailbox in mailboxes)
        {
            try
            {
                var password = protector.Unprotect(mailbox.ProtectedPassword);
                await client.ProcessUnreadAsync(mailbox, password, (mail, token) => processor.ProcessAsync(mailbox, mail, token), ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Helpdesk mailbox okunamadı. MailboxId={MailboxId}", mailbox.Id);
            }
        }
    }

    private static async Task ReactivateExpiredTicketsAsync(AppDataContext db, CancellationToken ct)
    {
        var now = DateTimeOffset.Now;
        var tickets = await db.HelpdeskTickets.Where(x => !x.IsDeleted && x.IsSuspended && x.SuspendedUntil <= now).ToListAsync(ct);
        foreach (var ticket in tickets)
        {
            ticket.IsSuspended = false;
            ticket.SuspendedUntil = null;
            ticket.UpdatedDate = now;
            ticket.UpdatedUser = 0;
            db.HelpdeskTicketHistories.Add(new HelpdeskTicketHistory { TicketId = ticket.Id, Action = "AutoUnsuspended", Description = "Askı süresi dolduğu için otomatik açıldı.", CreatedDate = now, CreatedUser = 0 });
        }
        if (tickets.Count > 0) await db.SaveChangesAsync(ct);
    }
}
