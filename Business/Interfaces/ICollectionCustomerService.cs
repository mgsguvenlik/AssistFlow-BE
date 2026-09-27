using Core.Common;
using Model.Dtos.Crm.Collections;

namespace Business.Interfaces;

public interface ICollectionCustomerService
{
    Task<ResponseModel<CollectionCustomerCard>> GetAsync(long id, CancellationToken ct);
    Task<ResponseModel<PagedResult<CollectionCustomerNoteItem>>> NotesAsync(long id, CollectionRateHistoryQuery query, CancellationToken ct);
    Task<ResponseModel<PagedResult<CollectionCustomerPaymentItem>>> PaymentsAsync(long id, CollectionRateHistoryQuery query, CancellationToken ct);
    Task<ResponseModel<long>> AddNoteAsync(long id, CollectionCustomerNoteCreate command, long actor, CancellationToken ct);
    Task<ResponseModel<long>> ChangeNoteAsync(long id, long noteId, CollectionCustomerNoteChange command, bool delete, long actor, CancellationToken ct);
}

