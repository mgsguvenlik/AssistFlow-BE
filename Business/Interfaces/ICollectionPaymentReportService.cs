using Core.Common;
using Model.Dtos.Crm.Collections;

namespace Business.Interfaces;

public interface ICollectionPaymentReportService
{
    Task<ResponseModel<PagedResult<CollectionPaymentReportItem>>> GetPageAsync(
        CollectionPaymentReportQuery query,
        CancellationToken cancellationToken = default);
}
