using Model.Concrete.Helpdesk;

namespace Business.Interfaces.Helpdesk;

public interface IHelpdeskIncomingMailProcessor
{
    Task<bool> ProcessAsync(HelpdeskMailbox mailbox, HelpdeskInboundMail mail, CancellationToken cancellationToken = default);
}
