using Model.Concrete.Helpdesk;

namespace Business.Interfaces.Helpdesk;

public sealed class HelpdeskInboundMail
{
    public string MessageId { get; init; } = string.Empty;
    public string? InReplyTo { get; init; }
    public string? References { get; init; }
    public string FromName { get; init; } = string.Empty;
    public string FromAddress { get; init; } = string.Empty;
    public string ToRecipients { get; init; } = string.Empty;
    public string? CcRecipients { get; init; }
    public string? BccRecipients { get; init; }
    public string Subject { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public DateTimeOffset MailDate { get; init; }
}

public interface IHelpdeskMailboxClient
{
    Task ProcessUnreadAsync(
        HelpdeskMailbox mailbox,
        string password,
        Func<HelpdeskInboundMail, CancellationToken, Task<bool>> handler,
        CancellationToken cancellationToken = default);
}
