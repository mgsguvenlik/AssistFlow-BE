using Core.Common;
using Microsoft.AspNetCore.Http;
using Model.Dtos.Crm.Collections;

namespace Business.Interfaces;
public interface ICollectionBankLoadService
{
    Task<ResponseModel<PagedResult<CollectionBankLoadItem>>> ListAsync(int page, int size, CancellationToken ct);
    Task<ResponseModel<CollectionBankLoadItem>> GetAsync(long id, CancellationToken ct);
    Task<ResponseModel<PagedResult<CollectionBankLoadRowItem>>> RowsAsync(long id, int page, int size, string? status, CancellationToken ct);
    Task<ResponseModel<long>> UploadAsync(IFormFile file, string type, DateOnly period, long actor, CancellationToken ct);
    Task<ResponseModel<int>> ProcessAsync(long id, string version, bool apply, long actor, CancellationToken ct);
    Task<ResponseModel<PagedResult<CollectionBankContractOption>>> ContractsAsync(long id, long rowId, int page, int size, CancellationToken ct);
    Task<ResponseModel<int>> SelectContractAsync(long id, long rowId, CollectionBankLoadSelect command, long actor, CancellationToken ct);
}
