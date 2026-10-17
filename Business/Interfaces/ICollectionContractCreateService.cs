using Core.Common;
using Model.Dtos.Crm.Collections;

namespace Business.Interfaces;

public interface ICollectionContractCreateService
{
    Task<ResponseModel<CollectionContractCreated>> CreateAsync(CollectionContractCreate command, long actorId,
        CancellationToken cancellationToken = default);
}
