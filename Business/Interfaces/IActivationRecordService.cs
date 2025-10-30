using Core.Common;
using Core.Enums;
using Model.Concrete.WorkFlows;
using Model.Dtos.WorkFlowDtos.WorkFlowActivityRecord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.Interfaces
{
    public interface IActivationRecordService
    {
        Task LogAsync(WorkFlowActivityRecord entry, CancellationToken ct = default);

        Task LogAsync(
            WorkFlowActionType type,
            string? requestNo,
            long? workFlowId,
            string? fromStepCode,
            string? toStepCode,
            string? summary,
            object? payload,
            CancellationToken ct = default);
        Task<ResponseModel<List<WorkFlowActivityRecorGetDto>>> GetLatestActivityRecordByRequestNoAsync(string requestNo);
        Task<ResponseModel<List<WorkFlowActivityRecorGetDto>>> GetUserActivity(int userId);
    }
}
