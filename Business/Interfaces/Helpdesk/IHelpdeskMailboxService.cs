using Core.Common;
using Model.Dtos.Helpdesk;

namespace Business.Interfaces.Helpdesk;

public interface IHelpdeskMailboxService
{
    Task<ResponseModel<List<HelpdeskMailboxDto>>> GetMailboxesAsync(CancellationToken cancellationToken = default);
    Task<ResponseModel<HelpdeskMailboxDto>> CreateMailboxAsync(HelpdeskMailboxCreateDto dto, CancellationToken cancellationToken = default);
    Task<ResponseModel<HelpdeskMailboxDto>> UpdateMailboxAsync(long id, HelpdeskMailboxUpdateDto dto, CancellationToken cancellationToken = default);
    Task<ResponseModel<bool>> DeleteMailboxAsync(long id, CancellationToken cancellationToken = default);
    Task<ResponseModel<List<HelpdeskMailRuleDto>>> GetRulesAsync(long mailboxId, CancellationToken cancellationToken = default);
    Task<ResponseModel<HelpdeskMailRuleDto>> CreateRuleAsync(HelpdeskMailRuleCreateDto dto, CancellationToken cancellationToken = default);
    Task<ResponseModel<HelpdeskMailRuleDto>> UpdateRuleAsync(long id, HelpdeskMailRuleUpdateDto dto, CancellationToken cancellationToken = default);
    Task<ResponseModel<bool>> DeleteRuleAsync(long id, CancellationToken cancellationToken = default);
}
