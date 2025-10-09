using Core.Common;
using Model.Dtos.ProgressApprover;

namespace Business.Interfaces
{
    public interface IProgressApproverService
    {
         Task<ResponseModel<List<ProgressApproverGetDto>>> GetByCustomerIdAsync(long customerId, CancellationToken cancellationToken);
    }
}
