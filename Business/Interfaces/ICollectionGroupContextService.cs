using Core.Common;
using Model.Dtos.Crm.Collections;

namespace Business.Interfaces;

public interface ICollectionGroupContextService
{
    Task<ResponseModel<CollectionGroupContext>> GetAsync(long groupId, CancellationToken cancellationToken = default);
    Task<ResponseModel<CollectionGroupContext>> ResolveAccountAsync(string accountNo, CancellationToken cancellationToken = default);
}
