using Core.Common;
using Model.Dtos.Crm.Collections;

namespace Business.Interfaces;

public interface ICollectionInvoiceCommandService
{
    Task<ResponseModel<CollectionInvoiceCommit>> ExecuteAsync(long invoiceId, long? paymentId, string kind,
        CollectionInvoiceCommand command, long actor, CancellationToken ct);
}
