using Core.Common;
using Model.Dtos.Crm.Collections;

namespace Business.Interfaces;

public interface ICollectionTrackingService
{
    Task<ResponseModel<PagedResult<CollectionTrackingItem>>> GetPageAsync(CollectionTrackingQuery query,
        CancellationToken cancellationToken = default);
    ResponseModel<IAsyncEnumerable<CollectionTrackingItem>> GetExportRows(CollectionTrackingQuery query);
}
