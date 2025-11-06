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
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Mail enqueue başarısız: {Subject}", item.Subject);
        }
    }
}
