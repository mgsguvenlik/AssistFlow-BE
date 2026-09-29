using Core.Common;
using Model.Dtos.Crm.Collections;

namespace Business.Interfaces;

public interface ICollectionInvoiceService
{
    Task<ResponseModel<PagedResult<CollectionInvoiceItem>>> ListAsync(CollectionInvoiceQuery query, CancellationToken ct);
    Task<ResponseModel<CollectionInvoiceDetail>> GetAsync(long id, CancellationToken ct);
    Task<ResponseModel<PagedResult<CollectionInvoicePaymentItem>>> PaymentsAsync(long id, CollectionRateHistoryQuery query, CancellationToken ct);
}
