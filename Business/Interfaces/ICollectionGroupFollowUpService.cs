using Core.Common;
using Model.Dtos.Crm.Collections;

namespace Business.Interfaces;

public interface ICollectionGroupFollowUpService
{
    Task<ResponseModel<CollectionGroupFollowUpItem>> GetAsync(long contractId, DateOnly period,
        CancellationToken cancellationToken = default);
    Task<ResponseModel<CollectionGroupFollowUpItem>> SaveAsync(long contractId,
        CollectionGroupFollowUpUpdate command, long actorId, CancellationToken cancellationToken = default);
}
