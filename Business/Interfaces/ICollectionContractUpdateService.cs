using Core.Common;
using Model.Dtos.Crm.Collections;

namespace Business.Interfaces;

public interface ICollectionContractUpdateService
{
    Task<ResponseModel<CollectionContractIdentityUpdated>> UpdateIdentityAsync(long id,
        CollectionContractIdentityUpdate command, long actorId, CancellationToken cancellationToken = default);
}
