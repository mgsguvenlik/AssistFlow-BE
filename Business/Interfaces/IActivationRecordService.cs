using Core.Common;
using Core.Enums;
using Model.Concrete.WorkFlows;
using Model.Dtos.WorkFlowDtos.WorkFlowActivityRecord;

namespace Business.Interfaces
{
    public interface IActivationRecordService
    {
        Task LogAsync(WorkFlowActivityRecord entry, CancellationToken ct = default);

        Task LogAsync(
            WorkFlowActionType type,
            string? requestNo,
            long? workFlowId,
            long? customerId,
            string? fromStepCode,
            string? toStepCode,
            string? summary,
            object? payload,

            CancellationToken ct = default);
        Task<ResponseModel<List<WorkFlowActivityRecorGetDto>>> GetLatestActivityRecordByRequestNoAsync(string requestNo);
        Task<ResponseModel<PagedResult<WorkFlowActivityRecorGetDto>>> GetUserActivity(int userId, QueryParams q);
        Task<ResponseModel<PagedResult<WorkFlowActivityGroupDto>>> GetUserActivityGroupedByRequestNo(int userId, QueryParams q, int perGroupTake = 50);
        Task<ResponseModel<PagedResult<WorkFlowActivityRecorGetDto>>> GetCustomerActivity(int customerId, QueryParams q);
    }
}
