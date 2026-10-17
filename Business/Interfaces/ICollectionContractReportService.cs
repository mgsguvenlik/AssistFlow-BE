using Core.Common;
using Model.Dtos.Crm.Collections;

namespace Business.Interfaces;

public interface ICollectionContractReportService
{
    Task<ResponseModel<PagedResult<CollectionContractReportItem>>> GetPageAsync(
        CollectionContractReportQuery query,
        CancellationToken cancellationToken = default);
}
