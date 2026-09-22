using Business.Services.Crm.Collections;
using Core.Common;
using Model.Dtos.Crm.Collections;

namespace Business.Interfaces;

public interface ICollectionPaymentService
{
    Task<ResponseModel<CollectionPaymentCommitResult>> CreateAsync(long contractId, CollectionPaymentCreate command,
        long actorId, CancellationToken cancellationToken = default);
    Task<ResponseModel<CollectionPaymentCommitResult>> UpdateAsync(long contractId, long paymentId,
        CollectionPaymentUpdate command, long actorId, CancellationToken cancellationToken = default);
    Task<ResponseModel<CollectionPaymentCommitResult>> DeleteAsync(long contractId, long paymentId,
        CollectionPaymentDelete command, long actorId, CancellationToken cancellationToken = default);
    Task<ResponseModel<CollectionPaymentBatchResult>> CreateBatchAsync(CollectionPaymentBatchCreate command,
        long actorId, CancellationToken cancellationToken = default);
}
