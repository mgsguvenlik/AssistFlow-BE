using Core.Common;
using Model.Dtos.Helpdesk;

namespace Business.Interfaces.Helpdesk;

public interface IHelpdeskTicketService
{
    Task<ResponseModel<List<HelpdeskTicketListItemDto>>> GetListAsync(CancellationToken cancellationToken = default);
    Task<ResponseModel<HelpdeskTicketDetailDto>> GetAsync(long id, CancellationToken cancellationToken = default);
    Task<ResponseModel<HelpdeskTicketDetailDto>> CreateAsync(HelpdeskTicketCreateDto dto, CancellationToken cancellationToken = default);
    Task<ResponseModel<HelpdeskTicketDetailDto>> AssignAsync(long id, HelpdeskAssignmentDto dto, CancellationToken cancellationToken = default);
    Task<ResponseModel<HelpdeskTicketDetailDto>> ChangeStatusAsync(long id, HelpdeskStatusChangeDto dto, CancellationToken cancellationToken = default);
    Task<ResponseModel<HelpdeskTicketDetailDto>> ChangePriorityAsync(long id, HelpdeskPriorityChangeDto dto, CancellationToken cancellationToken = default);
    Task<ResponseModel<HelpdeskTicketDetailDto>> SuspendAsync(long id, HelpdeskSuspendDto dto, CancellationToken cancellationToken = default);
    Task<ResponseModel<HelpdeskTicketDetailDto>> UnsuspendAsync(long id, CancellationToken cancellationToken = default);
    Task<ResponseModel<HelpdeskCommentDto>> AddCommentAsync(long id, HelpdeskCommentCreateDto dto, CancellationToken cancellationToken = default);
    Task<ResponseModel<List<HelpdeskTicketMailDto>>> GetMailsAsync(long id, CancellationToken cancellationToken = default);
    Task<ResponseModel<List<HelpdeskCommentDto>>> GetCommentsAsync(long id, CancellationToken cancellationToken = default);
    Task<ResponseModel<List<HelpdeskHistoryDto>>> GetHistoryAsync(long id, CancellationToken cancellationToken = default);
    Task<ResponseModel<HelpdeskTicketMailDto>> ReplyAsync(long id, HelpdeskReplyDto dto, CancellationToken cancellationToken = default);
}
