using Business.Interfaces;
using Business.UnitOfWork;
using Core.Enums;
using Microsoft.Extensions.Logging;
using Model.Concrete;
using Model.Concrete.WorkFlows;

public class MailPushService : IMailPushService
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<MailPushService> _log;

    public MailPushService(IUnitOfWork uow, ILogger<MailPushService> log)
    {
        _uow = uow;
        _log = log;
    }

    public async Task EnqueueAsync(MailOutbox item)
    {
        try
        {
            item.Status = MailOutboxStatus.Pending;
            item.NextAttemptAt ??= DateTime.Now;
            await _uow.Repository.AddAsync(item);
            await _uow.Repository.CompleteAsync();

            // Aktivite logu
            //await _uow.Repository.AddAsync(new WorkFlowActivityRecord
            //{
            //    RequestNo = item.RequestNo,
            //    FromStepCode = item.FromStepCode, 
            //    ToStepCode = item.ToStepCode,
            //    ActionType = WorkFlowActionType.MailQueued,
            //    Summary = $"Mail kuyruğa alındı: {item.Subject} → {item.ToRecipients}",
            //    OccurredAtUtc = DateTime.Now
            //});
            //await _uow.Repository.CompleteAsync();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Mail enqueue başarısız: {Subject}", item.Subject);

            //await _uow.Repository.AddAsync(new WorkFlowActivityRecord
            //{
            //    RequestNo = item.RequestNo,
            //    FromStepCode = item.FromStepCode,
            //    ToStepCode = item.ToStepCode,
            //    ActionType = WorkFlowActionType.MailQueueFailed,
            //    Summary = $"Mail kuyruğa alınamadı: {ex.Message}",
            //    OccurredAtUtc = DateTime.Now
            //});
            //await _uow.Repository.CompleteAsync();

            // akışı ASLA düşürmeyelim
        }
    }
}
