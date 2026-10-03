using Business.Interfaces;
using Core.Settings.Concrete;
using Data.Concrete.EfCore.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Model.Concrete.Collections;

namespace Business.Services.Crm.Collections;

/// <summary>Claims in SQL, commits before HTTP. Stale claims become unknown, never automatically resent.</summary>
public sealed class CollectionSmsDispatcher(IServiceScopeFactory scopes, IOptions<SmsServiceOptions> options,
    IOptions<CollectionReadOptions> collectionOptions, ILogger<CollectionSmsDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (options.Value.CollectionSmsEnabled && collectionOptions.Value.Enabled)
                {
                    for (var i = 0; i < 20 && !stoppingToken.IsCancellationRequested; i++)
                    {
                        using var scope = scopes.CreateScope();
                        if (!await ProcessNextAsync(scope.ServiceProvider.GetRequiredService<AppDataContext>(),
                            scope.ServiceProvider.GetRequiredService<ISmsSender>(), stoppingToken)) break;
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                // No phone/message/credentials in general application logs.
                logger.LogError("Tahsilat SMS kuyruğu işlenemedi. Hata türü: {ErrorType}", ex.GetType().Name);
            }
            try { await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    public static async Task<bool> ProcessNextAsync(AppDataContext db, ISmsSender sender, CancellationToken ct = default, long? onlyNotificationId = null)
    {
        long notificationId = 0;
        long attemptId = 0;
        string phone = "", message = "";
        bool simulation = false;
        var staleBefore = DateTimeOffset.UtcNow.AddMinutes(-2);
        var targetId = onlyNotificationId ?? 0;
        var found = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            notificationId = attemptId = 0;
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
            var item = await db.Set<CollectionSmsNotification>().FromSqlInterpolated($"""
                SELECT TOP (1) * FROM collection.SmsNotification WITH (UPDLOCK, READPAST, READCOMMITTEDLOCK)
                WHERE (Status = 0 OR (Status = 1 AND UpdatedDate < {staleBefore})) AND ({targetId} = 0 OR Id = {targetId})
                ORDER BY UpdatedDate, Id
                """).AsTracking().SingleOrDefaultAsync(ct);
            if (item is null) { await tx.CommitAsync(ct); return false; }
            var attempt = await db.Set<CollectionSmsAttempt>().SingleAsync(x => x.NotificationId == item.Id && x.Sequence == item.AttemptCount, ct);
            if (item.Status == CollectionSmsStatus.Sending)
            {
                Complete(item, attempt, CollectionSmsStatus.Unknown,
                    "Gönderim sırasında işlem kesildi. Servis kontrolü yapılmadan yeniden gönderilmez.", "INTERRUPTED_SEND", null);
                await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); db.ChangeTracker.Clear(); return true;
            }
            var current = await db.Set<CollectionContractRatePeriod>().AsNoTracking()
                .Where(x => x.Id == item.RatePeriodId && x.ContractId == item.ContractId && !x.IsDeleted && !x.Contract.IsDeleted)
                .Select(x => new { x.Amount, x.EffectiveFrom, x.BillingBehavior }).SingleOrDefaultAsync(ct);
            if (current is null || current.Amount != item.NewAmount || current.EffectiveFrom != item.EffectiveDate
                || current.BillingBehavior != CollectionBillingBehavior.Billable)
            {
                Complete(item, attempt, CollectionSmsStatus.Failed,
                    "Zam kaydı kaldırılmış veya değiştirilmiş; eski bildirim gönderilmedi.", "RATE_CHANGED", null);
                await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); db.ChangeTracker.Clear(); return true;
            }
            item.Status = attempt.Status = CollectionSmsStatus.Sending;
            item.UpdatedDate = DateTimeOffset.UtcNow;
            attempt.StartedDate = item.UpdatedDate;
            notificationId = item.Id; attemptId = attempt.Id;
            phone = attempt.Phone; message = attempt.Message; simulation = attempt.IsSimulation;
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); db.ChangeTracker.Clear(); return true;
        });
        if (!found || attemptId == 0) return found;
        SmsSendResult result;
        try
        {
            // A message queued in simulation can never turn live after a configuration change.
            result = simulation ? new(SmsSendStatus.Simulation, "Simülasyon tamamlandı; gerçek SMS gönderilmedi.")
                : await sender.SendAsync(phone, message, $"collection-sms-{attemptId}", ct);
        }
        catch (Exception)
        {
            result = new(SmsSendStatus.Unknown, "SMS sonucu doğrulanamadı. Servis kontrolü yapılmadan tekrar gönderilemez.", ErrorCode: "PROVIDER_RESULT_UNKNOWN");
        }
        // If persistence fails after HTTP, leave Sending; recovery will conservatively mark Unknown.
        // Retrying this SQL step never repeats the provider call.
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var item = await db.Set<CollectionSmsNotification>().SingleAsync(x => x.Id == notificationId, ct);
            var attempt = await db.Set<CollectionSmsAttempt>().SingleAsync(x => x.Id == attemptId, ct);
            if (item.Status == CollectionSmsStatus.Sending && attempt.Status == CollectionSmsStatus.Sending)
            {
                var status = result.Status switch
                {
                    SmsSendStatus.Accepted => CollectionSmsStatus.Accepted,
                    SmsSendStatus.Simulation => CollectionSmsStatus.Simulation,
                    SmsSendStatus.Rejected => CollectionSmsStatus.Failed,
                    _ => CollectionSmsStatus.Unknown
                };
                Complete(item, attempt, status, result.Message, result.ErrorCode, result.PackageId);
                attempt.IsSimulation = status == CollectionSmsStatus.Simulation;
                await db.SaveChangesAsync(ct);
            }
            await tx.CommitAsync(ct); db.ChangeTracker.Clear();
        });
        return true;
    }

    private static void Complete(CollectionSmsNotification item, CollectionSmsAttempt attempt,
        CollectionSmsStatus status, string message, string? code, string? package)
    {
        item.Status = attempt.Status = status;
        item.UpdatedDate = DateTimeOffset.UtcNow;
        attempt.CompletedDate = item.UpdatedDate;
        attempt.ResultMessage = message;
        attempt.ErrorCode = code;
        attempt.PackageId = package;
    }
}
