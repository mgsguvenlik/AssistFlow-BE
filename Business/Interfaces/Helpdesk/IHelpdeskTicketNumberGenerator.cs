namespace Business.Interfaces.Helpdesk;

public interface IHelpdeskTicketNumberGenerator
{
    Task<string> NextAsync(CancellationToken cancellationToken = default);
}
