using Core.Common;
using Model.Dtos.Crm.Collections;

namespace Business.Interfaces;

public interface ICollectionSubscriptionService
{
    Task<ResponseModel<CollectionSubscriptionChanged>> ChangeAsync(long id, CollectionSubscriptionChange command,
        long actorId, CancellationToken cancellationToken = default);
}
