using Core.Common;
using Model.Dtos.Crm.Collections;

namespace Business.Interfaces;

public interface ICollectionContractReadService
{
    Task<ResponseModel<PagedResult<CollectionContractListItem>>> GetPageAsync(
        CollectionContractQuery query, CancellationToken cancellationToken = default);
    Task<ResponseModel<CollectionContractDetail>> GetDetailAsync(long id, CancellationToken cancellationToken = default);
    Task<ResponseModel<PagedResult<CollectionRateHistoryItem>>> GetHistoryAsync(long id,
        CollectionRateHistoryQuery query, CancellationToken cancellationToken = default);
}
