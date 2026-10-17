using Core.Common;
using Microsoft.AspNetCore.Http;
using Model.Dtos.Crm.Collections;

namespace Business.Interfaces;

public interface ICollectionContractAttachmentService
{
    Task<ResponseModel<PagedResult<CollectionContractAttachmentItem>>> GetCustomerPageAsync(long customerId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<ResponseModel> UploadCustomerAsync(long customerId, IFormFile file, Guid requestId, long actorId, CancellationToken cancellationToken = default);
    Task<ResponseModel> RemoveCustomerAsync(long customerId, long attachmentId, long actorId, CancellationToken cancellationToken = default);
    Task<ResponseModel> RemoveAsync(long contractId, long attachmentId, long actorId,
        CancellationToken cancellationToken = default);
    Task<ResponseModel<PagedResult<CollectionContractAttachmentItem>>> GetPageAsync(long contractId,
        int page, int pageSize, CancellationToken cancellationToken = default);
    Task<ResponseModel<CollectionContractAttachmentItem>> UploadAsync(long contractId, IFormFile file,
        long actorId, CancellationToken cancellationToken = default);
}
