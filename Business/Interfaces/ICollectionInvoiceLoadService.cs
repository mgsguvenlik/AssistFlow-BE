using Core.Common;
using Microsoft.AspNetCore.Http;
using Model.Dtos.Crm.Collections;

namespace Business.Interfaces;

public interface ICollectionInvoiceLoadService
{
    Task<ResponseModel<long>> UploadAsync(IFormFile file, string type, long actor, CancellationToken ct);
    Task<ResponseModel<PagedResult<CollectionInvoiceLoadItem>>> ListAsync(int page, int pageSize, CancellationToken ct);
    Task<ResponseModel<CollectionInvoiceLoadItem>> GetAsync(long id, CancellationToken ct);
    Task<ResponseModel<PagedResult<CollectionInvoiceLoadRowItem>>> RowsAsync(long id, int page, int pageSize, string? status, CancellationToken ct);
    Task<ResponseModel<long>> ProcessAsync(long id, byte[] version, bool apply, long actor, CancellationToken ct);
}
