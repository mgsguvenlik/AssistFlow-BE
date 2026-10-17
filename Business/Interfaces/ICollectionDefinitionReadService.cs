using Core.Common;
using Model.Dtos.Crm.Collections;

namespace Business.Interfaces;

public interface ICollectionDefinitionReadService
{
    Task<ResponseModel<PagedResult<CollectionDefinitionItem>>> GetPageAsync(CollectionDefinitionQuery query,
        CancellationToken cancellationToken = default);
}
