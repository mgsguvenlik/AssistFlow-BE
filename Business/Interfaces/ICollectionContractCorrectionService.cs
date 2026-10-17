using Core.Common;
using Model.Dtos.Crm.Collections;
namespace Business.Interfaces;
public interface ICollectionContractCorrectionService
{
    Task<ResponseModel> ExecuteAsync(long id, CollectionContractCorrectionCommand command, long actor, CancellationToken ct);
    Task<ResponseModel<PagedResult<CollectionContractCorrectionItem>>> HistoryAsync(long id, int page, int size, CancellationToken ct);
}
