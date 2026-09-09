using Core.Common;
using Model.Dtos.Crm.Collections;

namespace Business.Interfaces;

public interface ICollectionContractReadService
{
    Task<ResponseModel<PagedResult<CollectionContractListItem>>> GetPageAsync(
        CollectionContractQuery query, CancellationToken cancellationToken = default);
    Task<ResponseModel<CollectionContractListItem>> GetDetailAsync(long id, CancellationToken cancellationToken = default);
}
