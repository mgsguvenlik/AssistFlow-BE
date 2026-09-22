using Core.Common;
using Model.Dtos.Crm.Collections;

namespace Business.Interfaces;

public interface ICollectionRateChangeService
{
    Task<ResponseModel<CollectionRateChanged>> ChangeAsync(long id, CollectionRateChange command,
        long actorId, CancellationToken cancellationToken = default);
}
