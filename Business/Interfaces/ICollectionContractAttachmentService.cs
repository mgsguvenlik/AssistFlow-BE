using Core.Common;
using Microsoft.AspNetCore.Http;
using Model.Dtos.Crm.Collections;

namespace Business.Interfaces;

public interface ICollectionContractAttachmentService
{
    Task<ResponseModel<PagedResult<CollectionContractAttachmentItem>>> GetPageAsync(long contractId,
        int page, int pageSize, CancellationToken cancellationToken = default);
    Task<ResponseModel<CollectionContractAttachmentItem>> UploadAsync(long contractId, IFormFile file,
        long actorId, CancellationToken cancellationToken = default);
}
